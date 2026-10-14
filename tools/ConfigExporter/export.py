"""读取四行表头的 Excel 配置，校验后导出版本化二进制。仅使用标准库。"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from decimal import Decimal, InvalidOperation
import io
import json
import math
from pathlib import Path, PurePosixPath
import re
import shutil
import struct
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

NS = {"s": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
REL_NS = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
IDENTIFIER = re.compile(r"[A-Za-z_][A-Za-z0-9_]*\Z")
INTEGER = re.compile(r"[+-]?[0-9]+\Z")
CODES = {"string": 1, "int": 2, "long": 3, "bool": 4, "float": 5, "double": 6, "enum": 7}
SCOPES = ("Client", "Server", "Shared")
KEYWORDS = set("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while".split())


class ConfigError(ValueError):
    """配置格式或内容不合法。"""


@dataclass(frozen=True)
class Cell:
    text: str = ""
    kind: str = "text"
    formula: bool = False


@dataclass(frozen=True)
class Field:
    name: str
    base: str
    array: bool
    enum_name: str
    rule: str
    description: str
    column: int


@dataclass
class Table:
    scope: str
    name: str
    fields: list[Field]
    key_index: int
    rows: list[list]


def column_name(column: int) -> str:
    result = ""
    while column:
        column, digit = divmod(column - 1, 26)
        result = chr(65 + digit) + result
    return result


def fail(file: Path, sheet: str, row: int, column: int, field: str, value, message: str):
    raise ConfigError(f"{file} | 工作表 {sheet} | {column_name(column)}{row} | 字段 {field} | 值 {str(value)[:160]!r}：{message}")


def read_workbook(file: Path):
    """兼容共享字符串、内联字符串和普通数值，不读取公式缓存作为配置值。"""
    try:
        with zipfile.ZipFile(file) as archive:
            workbook = ET.fromstring(archive.read("xl/workbook.xml"))
            relationships = ET.fromstring(archive.read("xl/_rels/workbook.xml.rels"))
            targets = {r.get("Id"): r.get("Target") for r in relationships if r.get("TargetMode") != "External"}
            strings = []
            if "xl/sharedStrings.xml" in archive.namelist():
                strings = ["".join(t.text or "" for t in item.findall(".//s:t", NS)) for item in ET.fromstring(archive.read("xl/sharedStrings.xml")).findall("s:si", NS)]
            for sheet in workbook.findall("s:sheets/s:sheet", NS):
                name = sheet.get("name", "")
                if name.startswith("_"):
                    continue
                target = targets.get(sheet.get(f"{{{REL_NS}}}id"))
                if not target:
                    raise ConfigError(f"{file} | 工作表 {name}：缺少工作表引用")
                member = target.lstrip("/") if target.startswith("/") else "xl/" + target
                if ".." in PurePosixPath(member).parts:
                    raise ConfigError(f"{file} | 工作表 {name}：非法工作表路径")
                xml = ET.fromstring(archive.read(member))
                if xml.find("s:mergeCells/s:mergeCell", NS) is not None:
                    raise ConfigError(f"{file} | 工作表 {name}：数据页禁止合并单元格")
                cells = {}
                for c in xml.findall("s:sheetData/s:row/s:c", NS):
                    address = c.get("r", "")
                    match = re.fullmatch(r"([A-Z]+)([1-9][0-9]*)", address)
                    if not match:
                        raise ConfigError(f"{file} | 工作表 {name}：非法单元格地址 {address}")
                    column = 0
                    for letter in match[1]:
                        column = column * 26 + ord(letter) - 64
                    kind = c.get("t", "n")
                    v = c.find("s:v", NS)
                    value = v.text or "" if v is not None else ""
                    if kind == "s":
                        value = strings[int(value)]
                    elif kind == "inlineStr":
                        value = "".join(n.text or "" for n in c.findall("s:is//s:t", NS))
                    elif kind == "e":
                        fail(file, name, int(match[2]), column, "未知", value, "Excel 错误值")
                    cells[(int(match[2]), column)] = Cell(value, "text" if kind in ("s", "str", "inlineStr") else kind, c.find("s:f", NS) is not None)
                yield name, cells
    except (zipfile.BadZipFile, ET.ParseError, KeyError, IndexError, ValueError) as exc:
        if isinstance(exc, ConfigError):
            raise
        raise ConfigError(f"{file}：无法读取 Excel：{exc}") from exc


def load_enums(file: Path) -> dict:
    def unique(pairs):
        result = {}
        for name, value in pairs:
            if name in result:
                raise ConfigError(f"枚举配置出现重复名称：{name}")
            result[name] = value
        return result
    data = json.loads(file.read_text(encoding="utf-8-sig"), object_pairs_hook=unique)
    if not isinstance(data, dict):
        raise ConfigError("枚举配置必须是对象")
    for name, members in data.items():
        if not IDENTIFIER.fullmatch(name) or name in KEYWORDS or not isinstance(members, dict) or not members:
            raise ConfigError(f"非法枚举定义：{name}")
        for member, value in members.items():
            if not IDENTIFIER.fullmatch(member) or member in KEYWORDS or type(value) is not int or not -(2**31) <= value < 2**31:
                raise ConfigError(f"非法枚举成员：{name}.{member}")
        if len(set(members.values())) != len(members):
            raise ConfigError(f"枚举 {name} 的数值重复")
    return data


def parse_type(type_name: str, enums: dict):
    array = type_name.endswith("[]")
    scalar = type_name[:-2] if array else type_name
    enum_name = scalar[5:] if scalar.startswith("enum:") else ""
    base = "enum" if enum_name else scalar
    if base not in CODES or base == "enum" and enum_name not in enums:
        raise ConfigError(f"不支持或未定义的类型：{type_name}")
    return base, array, enum_name


def parse_scalar(value, kind: str, field: Field, enums: dict):
    base = field.base
    if base == "string":
        if not isinstance(value, str) or kind != "text":
            raise ConfigError("字符串必须使用文本单元格；数字 ID 请先设置文本格式")
        return value
    if base == "enum":
        if not isinstance(value, str) or kind != "text" or value not in enums[field.enum_name]:
            raise ConfigError(f"需要枚举名称：{', '.join(enums[field.enum_name])}")
        return enums[field.enum_name][value]
    if base == "bool":
        if type(value) is bool:
            return value
        if isinstance(value, str) and value in ("true", "false", "1", "0"):
            return value in ("true", "1")
        if type(value) is int and value in (0, 1):
            return bool(value)
        raise ConfigError("bool 只接受 true/false 或 1/0")
    if type(value) is bool or kind not in ("text", "n", "json"):
        raise ConfigError("需要数值")
    if base in ("int", "long"):
        if type(value) is int:
            number = value
        elif isinstance(value, str) and kind == "text" and INTEGER.fullmatch(value):
            number = int(value)
        elif isinstance(value, str) and kind == "n":
            number_decimal = Decimal(value)
            if not number_decimal.is_finite() or number_decimal != number_decimal.to_integral_value():
                raise ConfigError("需要完整整数")
            number = int(number_decimal)
        else:
            raise ConfigError("需要完整整数，不能填写小数或表达式")
        bits = 32 if base == "int" else 64
        if not -(2 ** (bits - 1)) <= number < 2 ** (bits - 1):
            raise ConfigError(f"超出 {base} 范围")
        if base == "long" and kind == "n" and abs(number) >= 10**15:
            raise ConfigError("超过 15 位的 long 必须按文本填写，避免 Excel 丢失精度")
        return number
    if isinstance(value, str) and value != value.strip():
        raise ConfigError("数值不能含首尾空格")
    number = float(value)
    if not math.isfinite(number):
        raise ConfigError("浮点数必须有限，禁止 NaN 和 Infinity")
    if base == "float":
        try:
            number = struct.unpack("<f", struct.pack("<f", number))[0]
        except OverflowError as exc:
            raise ConfigError("超出 float 范围") from exc
    return number


def parse_value(cell: Cell, field: Field, enums: dict):
    if cell.text == "":
        if field.rule != "optional":
            raise ConfigError("必填值为空")
        if field.array:
            return []
        if field.base == "string":
            return ""
        if field.base == "enum" and 0 not in enums[field.enum_name].values():
            raise ConfigError("可选枚举需要定义数值为 0 的成员")
        return False if field.base == "bool" else 0
    if field.array:
        if cell.kind != "text":
            raise ConfigError("数组必须在文本单元格填写 JSON 数组")
        def invalid_constant(value):
            raise ConfigError(f"数组中禁止 {value}")
        values = json.loads(cell.text, parse_constant=invalid_constant)
        if not isinstance(values, list):
            raise ConfigError("需要 JSON 数组")
        return [parse_scalar(v, "text" if isinstance(v, str) else "json", field, enums) for v in values]
    return parse_scalar(cell.text, cell.kind, field, enums)


def parse_table(file: Path, scope: str, name: str, cells: dict, enums: dict) -> Table:
    if not IDENTIFIER.fullmatch(name) or name in KEYWORDS:
        raise ConfigError(f"{file} | 工作表 {name}：表名必须是合法标识符")
    nonempty = {address: c for address, c in cells.items() if c.text != "" or c.formula}
    if not nonempty:
        raise ConfigError(f"{file} | 工作表 {name}：数据页为空，说明页请以 _ 开头")
    for (row, column), cell in nonempty.items():
        if cell.formula:
            fail(file, name, row, column, cells.get((1, column), Cell()).text, cell.text, "禁止公式")
    width = max(column for row, column in nonempty)
    fields = []
    for column in range(1, width + 1):
        field_name = cells.get((1, column), Cell()).text
        if not IDENTIFIER.fullmatch(field_name) or field_name in KEYWORDS:
            fail(file, name, 1, column, field_name, field_name, "字段名必须是合法且非关键字的标识符，不能有空列")
        if any(f.name == field_name for f in fields):
            fail(file, name, 1, column, field_name, field_name, "字段名重复")
        type_name = cells.get((2, column), Cell()).text
        try:
            base, array, enum_name = parse_type(type_name, enums)
        except ConfigError as exc:
            fail(file, name, 2, column, field_name, type_name, str(exc))
        rule = cells.get((3, column), Cell()).text or "required"
        if rule not in ("key", "required", "optional"):
            fail(file, name, 3, column, field_name, rule, "规则只能是 key/required/optional")
        if rule == "key" and (array or base not in ("string", "int")):
            fail(file, name, 3, column, field_name, rule, "主键只能使用 string 或 int")
        if rule == "optional" and base == "enum" and not array and 0 not in enums[enum_name].values():
            fail(file, name, 3, column, field_name, rule, "可选枚举需要定义数值为 0 的成员")
        fields.append(Field(field_name, base, array, enum_name, rule, cells.get((4, column), Cell()).text, column))
    keys = [i for i, f in enumerate(fields) if f.rule == "key"]
    if len(keys) != 1:
        raise ConfigError(f"{file} | 工作表 {name}：必须且只能声明一个 key 字段")
    key_index = keys[0]
    rows, seen = [], {}
    for row in sorted({row for row, column in nonempty if row >= 5}):
        values = []
        for field in fields:
            cell = cells.get((row, field.column), Cell())
            try:
                value = parse_value(cell, field, enums)
            except (ValueError, TypeError, OverflowError, InvalidOperation) as exc:
                fail(file, name, row, field.column, field.name, cell.text, str(exc))
            if field.rule == "key" and isinstance(value, str) and value != value.strip():
                fail(file, name, row, field.column, field.name, value, "字符串主键不能含首尾空白")
            values.append(value)
        key = values[key_index]
        if key in seen:
            fail(file, name, row, fields[key_index].column, fields[key_index].name, key, f"主键重复，首次出现在第 {seen[key]} 行")
        seen[key] = row
        rows.append(values)
    rows.sort(key=lambda row: row[key_index].encode("utf-16-be") if isinstance(row[key_index], str) else row[key_index])
    return Table(scope, name, fields, key_index, rows)


def encode(table: Table, enums: dict) -> bytes:
    """长度均为字节数，所有数值统一小端；字符串使用严格 UTF-8。"""
    stream = io.BytesIO()
    def uint(value):
        stream.write(struct.pack("<I", value))
    def string(value):
        data = value.encode("utf-8")
        uint(len(data))
        stream.write(data)
    def scalar(value, field):
        if field.base == "string":
            string(value)
        else:
            stream.write(struct.pack({"int": "<i", "long": "<q", "bool": "<?", "float": "<f", "double": "<d", "enum": "<i"}[field.base], value))
    stream.write(b"GSCF")
    stream.write(struct.pack("<H", 1))
    string(table.scope)
    string(table.name)
    uint(len(table.fields))
    uint(len(table.rows))
    uint(table.key_index)
    for field in table.fields:
        string(field.name)
        stream.write(bytes([CODES[field.base], int(field.array) | (2 if field.rule == "optional" else 0)]))
        string(field.enum_name)
        members = enums[field.enum_name] if field.enum_name else {}
        uint(len(members))
        for member, value in sorted(members.items()):
            string(member)
            stream.write(struct.pack("<i", value))
    for row in table.rows:
        for field, value in zip(table.fields, row):
            if field.array:
                uint(len(value))
                for item in value:
                    scalar(item, field)
            else:
                scalar(value, field)
    return stream.getvalue()


def pascal_name(name: str) -> str:
    parts = [part for part in name.split("_") if part]
    result = "".join(part[0].upper() + part[1:] for part in parts)
    if not IDENTIFIER.fullmatch(result):
        raise ConfigError(f"名称 {name} 无法转换为 C# 大驼峰标识符")
    return result


def class_name(name: str) -> str:
    return pascal_name(name) + "Config"


def generate_runtime(table: Table, name: str, enums: dict, member_names: list[str]) -> str:
    """生成与表结构对应的读取代码，不使用反射或平台相关序列化器。"""
    templates = Path(__file__).with_name("templates")
    text = (templates / "TableRuntime.cs.txt").read_text(encoding="utf-8")
    literal = lambda value: json.dumps(value, ensure_ascii=True)
    minimum = {"string": 4, "int": 4, "long": 8, "bool": 1, "float": 4, "double": 8, "enum": 4}
    row_size = sum(4 if f.array else minimum[f.base] for f in table.fields)
    header = f"                int count = reader.ReadHeader({literal(table.scope)}, {literal(table.name)}, {len(table.fields)}, {table.key_index}, {row_size});"
    checks, reads, validation = [], [], []
    def cs_type(f):
        result = f.enum_name or f.base
        return "global::GameSync.Config." + result if f.enum_name in member_names else result
    def scalar(f):
        method = {"string": "ReadString", "int": "ReadInt32", "long": "ReadInt64", "bool": "ReadBoolean", "float": "ReadSingle", "double": "ReadDouble"}
        if f.base == "enum":
            values = ", ".join(str(v) for _, v in sorted(enums[f.enum_name].items()))
            return f"({cs_type(f)})reader.ReadEnum(new int[] {{ {values} }})"
        return "reader." + method[f.base] + "()"
    for f in table.fields:
        members = sorted(enums[f.enum_name].items()) if f.enum_name else []
        names = ", ".join(literal(n) for n, _ in members)
        values = ", ".join(str(v) for _, v in members)
        flags = int(f.array) | (2 if f.rule == "optional" else 0)
        checks.append(f"                reader.ExpectField({literal(f.name)}, {CODES[f.base]}, {flags}, {literal(f.enum_name)}, new string[] {{ {names} }}, new int[] {{ {values} }});")
        member = pascal_name(f.name)
        expression = scalar(f)
        if f.array:
            expression = f"reader.ReadArray<{cs_type(f)}>({minimum[f.base]}, () => {expression})"
        reads.append(f"                    row.{member} = {expression};")
        if f.base == "string" and not f.array and f.rule != "optional":
            validation.append(f"                    if (row.{member}.Length == 0)\n                    {{\n                        throw new InvalidDataException(\"必填字符串为空：{member}\");\n                    }}")
        if f.rule == "key" and f.base == "string":
            validation.append(f"                    if (row.{member} != row.{member}.Trim())\n                    {{\n                        throw new InvalidDataException(\"字符串 Key 含首尾空白。\");\n                    }}")
    checks.append(f"                reader.ValidateRows(count, {row_size});")
    key = table.fields[table.key_index]
    replacements = {
        "__KEY_TYPE__": key.base, "__CLASS__": name, "__READ_HEADER__": header,
        "__EXPECT_FIELDS__": "\n".join(checks), "__READ_VALUES__": "\n".join(reads),
        "__VALIDATE_VALUES__": "\n".join(validation), "__KEY_MEMBER__": pascal_name(key.name),
        "__COMPARER__": ", StringComparer.Ordinal" if key.base == "string" else "",
    }
    for marker, value in replacements.items():
        text = text.replace(marker, value)
    return text


def generate_csharp(tables: list[Table], enums: dict, bytes_directory="client/Res/Config") -> dict[str, bytes]:
    """每端生成带加载与查询接口的配置类、读取辅助类和引用的枚举。"""
    from html import escape
    generated = {}
    symbols = {}
    def reserve(name, owner):
        folded = name.casefold()
        if folded in symbols:
            raise ConfigError(f"C# 名称或文件名冲突：{owner} 与 {symbols[folded]} → {name}")
        symbols[folded] = owner
    reserve("ConfigManager", "配置层统一加载入口")
    if tables:
        reserve("ConfigBinaryReader", "二进制读取辅助类")
    for table in tables:
        reserve(class_name(table.name), "表 " + table.name)
    used_enums = sorted({f.enum_name for t in tables for f in t.fields if f.enum_name})
    for name in used_enums:
        reserve(name, "枚举 " + name)
        members = [pascal_name(m) for m in enums[name]]
        if len(set(members)) != len(members):
            raise ConfigError(f"枚举 {name} 的成员转为大驼峰后重名")
        if name in members:
            raise ConfigError(f"枚举成员不能与枚举类型同名：{name}")
    header = "// 此文件由导表工具自动生成，请勿手动修改。\nusing System;\nusing System.Collections.Generic;\nusing System.IO;\n\nnamespace GameSync.Config\n{\n"
    for table in tables:
        name = class_name(table.name)
        lines = [header, "    [Serializable]\n", f"    public sealed class {name}\n    {{\n"]
        member_names = [pascal_name(f.name) for f in table.fields]
        reserved = {"BytesPath", "Load", "LoadFromFile", "Get", "TryGet", "GetAll", "Find", "FindAll", "IsLoaded", "EnsureLoaded", "ConfigBinaryReader"}
        if len(set(member_names)) != len(member_names) or reserved.intersection(member_names):
            raise ConfigError(f"表 {table.name} 的字段转为大驼峰后重名")
        bytes_path = bytes_directory.rstrip("/") + "/" + table.name + ".bytes"
        literal = json.dumps(bytes_path, ensure_ascii=True)
        lines.append("        /// <summary>\n        /// 对应二进制配置路径，相对仓库根目录。\n        /// </summary>\n")
        lines.append(f"        public const string BytesPath = {literal};\n\n")
        for f in table.fields:
            member_name = pascal_name(f.name)
            if member_name == name:
                raise ConfigError(f"表 {table.name} 的字段 {f.name} 不能与生成类同名")
            if f.description:
                lines.append("        /// <summary>\n")
                lines.extend("        /// " + escape(line, quote=False) + "\n" for line in f.description.splitlines())
                lines.append("        /// </summary>\n")
            cs_type = f.enum_name or f.base
            # 仅当成员名遮蔽枚举类型时保留全局限定，普通情况使用简洁类型名。
            if f.enum_name in member_names:
                cs_type = "global::GameSync.Config." + f.enum_name
            if f.array:
                declaration = f"{cs_type}[] {member_name} {{ get; private set; }} = new {cs_type}[0];"
            elif f.base == "string":
                declaration = f"string {member_name} {{ get; private set; }} = string.Empty;"
            else:
                declaration = f"{cs_type} {member_name} {{ get; private set; }}"
            lines.append(f"        public {declaration}\n\n")
        lines.append(generate_runtime(table, name, enums, member_names))
        lines.append("    }\n}\n")
        generated[name + ".cs"] = "".join(lines).encode("utf-8")
    for name in used_enums:
        lines = [header, f"    public enum {name} : int\n    {{\n"]
        lines.extend(f"        {pascal_name(member)} = {value},\n" for member, value in sorted(enums[name].items()))
        lines.append("    }\n}\n")
        generated[name + ".cs"] = "".join(lines).encode("utf-8")
    manager = [
        "// 此文件由导表工具自动生成，请勿手动修改。\n\n",
        "namespace GameSync.Config\n{\n    public sealed partial class ConfigManager\n    {\n",
        "        /// <summary>自动生成的配置加载清单，由手写入口调用。</summary>\n",
        "        partial void LoadGenerated(string projectRoot)\n        {\n",
    ]
    if tables:
        manager.append("\n")
        manager.extend(f"            {class_name(table.name)}.LoadFromFile(projectRoot);\n" for table in sorted(tables, key=lambda t: t.name))
    else:
        manager.append("\n            // 当前本端没有配置表；后续导表会自动补充加载调用。\n")
    manager.append("        }\n    }\n}\n")
    generated["ConfigManager.Generated.cs"] = "".join(manager).encode("utf-8")
    return generated


def collect(source: Path, enums: dict):
    tables = []
    identities = {}
    for scope in SCOPES:
        folder = source / scope
        if not folder.exists():
            continue
        for file in sorted(folder.rglob("*.xlsx")):
            if file.name.startswith("~$"):
                continue
            for name, cells in read_workbook(file):
                # 共享表与任意一端的同名表会形成歧义，大小写碰撞也禁止。
                key = name.casefold()
                for previous_scope, previous_file in identities.get(key, []):
                    if scope == previous_scope or "Shared" in (scope, previous_scope):
                        raise ConfigError(f"{file} | 工作表 {name}：与 {previous_file} 的表名冲突")
                identities.setdefault(key, []).append((scope, file))
                tables.append(parse_table(file, scope, name, cells, enums))
    if not tables:
        raise ConfigError(f"{source}：没有可导出的配置表")
    return tables


def validate_output(output: Path):
    """输出目录只允许工具自己管理的文件，避免覆盖用户手动放入的资源。"""
    marker = output / ".config-exporter.json"
    if output.exists():
        if output.is_symlink() or not marker.is_file():
            raise ConfigError(f"{output}：已有目录不属于导表工具，拒绝覆盖")
        manifest = json.loads(marker.read_text(encoding="utf-8"))
        if not isinstance(manifest, dict) or manifest.get("format") != "GSCF" or manifest.get("version") != 1:
            raise ConfigError(f"{output}：输出目录标记无效")
        registered = manifest.get("files")
        if not isinstance(registered, list) or any(not isinstance(n, str) or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*(?:\.Generated)?\.(bytes|cs)", n) for n in registered):
            raise ConfigError(f"{output}：输出文件登记无效")
        actual = set()
        for p in output.iterdir():
            if p.is_symlink() or p.is_dir():
                raise ConfigError(f"{output}：输出目录包含链接或子目录，拒绝替换")
            # 本地 Unity 包会给生成脚本增加 meta，只接受对应已登记文件的 meta。
            if p.name.endswith(".meta") and p.name[:-5] in registered:
                continue
            actual.add(p.name)
        if actual != set(registered) | {marker.name}:
            raise ConfigError(f"{output}：输出目录存在未登记文件，拒绝替换")
def cleanup_temp(directory: Path, output: Path):
    if directory.exists():
        if directory.parent.resolve() != output.parent.resolve() or not directory.name.startswith(output.name + "."):
            raise ConfigError("暂存目录清理范围检查失败")
        shutil.rmtree(directory)


def publish(outputs: dict[Path, dict[str, bytes]]):
    """先暂存两端所有结果，替换异常时恢复旧目录；恢复失败保留备份。"""
    for output in outputs:
        validate_output(output)
    stages, backups, installed = {}, {}, []
    success = False
    try:
        for output, payloads in outputs.items():
            output.parent.mkdir(parents=True, exist_ok=True)
            stage = Path(tempfile.mkdtemp(prefix=output.name + ".stage-", dir=output.parent))
            stages[output] = stage
            for relative, data in payloads.items():
                (stage / relative).write_bytes(data)
                meta = output / (relative + ".meta")
                if meta.is_file():
                    shutil.copyfile(meta, stage / meta.name)
            (stage / ".config-exporter.json").write_text(json.dumps({"format": "GSCF", "version": 1, "files": sorted(payloads)}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        for output, stage in stages.items():
            if output.exists():
                backup = Path(tempfile.mkdtemp(prefix=output.name + ".backup-", dir=output.parent))
                backup.rmdir()
                output.rename(backup)
                backups[output] = backup
            stage.rename(output)
            installed.append(output)
        success = True
    except OSError as original:
        try:
            for output in reversed(list(stages)):
                if output in installed:
                    # 新目录先移回本次暂存路径，不递归删除输出目录。
                    output.rename(stages[output])
                if output in backups:
                    backup = backups[output]
                    backup.rename(output)
                    del backups[output]
        except OSError as recovery:
            raise ConfigError(f"输出替换失败，自动恢复也失败：{recovery}；旧文件保留在 {list(backups.values())}") from original
        raise
    finally:
        for output, stage in stages.items():
            cleanup_temp(stage, output)
        if success:
            for output, backup in backups.items():
                try:
                    cleanup_temp(backup, output)
                except OSError as exc:
                    print(f"警告：导出成功，但旧备份未清理：{backup}：{exc}", file=sys.stderr)


def run(source: Path, client_output: Path, server_output: Path, enums_file: Path, check=False, client_code=None, server_code=None, project_root=None):
    source = source.resolve()
    code_paths = [p for p in (client_code, server_code) if p is not None]
    for output in (client_output, server_output, *code_paths):
        if output.is_symlink() or getattr(output, "is_junction", lambda: False)():
            raise ConfigError("输出目录不能是符号链接")
    client_output, server_output = client_output.resolve(), server_output.resolve()
    paths = [source, client_output, server_output, *(p.resolve() for p in code_paths)]
    for i, a in enumerate(paths):
        for b in paths[i + 1:]:
            if a == b or a in b.parents or b in a.parents:
                raise ConfigError("源表与两端输出目录不能相同或互相包含")
    enums = load_enums(enums_file)
    tables = collect(source, enums)
    outputs = {client_output: {}, server_output: {}}
    for table in tables:
        payload = encode(table, enums)
        for output in ([client_output] if table.scope == "Client" else [server_output] if table.scope == "Server" else [client_output, server_output]):
            outputs[output][table.name + ".bytes"] = payload
    # 两端代码与二进制共用同一批次的暂存和回滚，避免生成结果不匹配。
    for output, scope in ((client_code, "Client"), (server_code, "Server")):
        selected = [t for t in tables if t.scope in (scope, "Shared")]
        binary_output = client_output if scope == "Client" else server_output
        import os
        root = project_root or Path(__file__).resolve().parents[2]
        try:
            bytes_directory = Path(os.path.relpath(binary_output, root)).as_posix() if output is not None else scope.lower() + "/Res/Config"
        except ValueError as exc:
            raise ConfigError("C# 配置路径无法相对仓库根目录表示，请勿跨盘输出") from exc
        code = generate_csharp(selected, enums, bytes_directory)
        # Unity 与 .NET 分别编译本端配置，避免同名表和加载清单冲突。
        condition = "UNITY_5_3_OR_NEWER" if scope == "Client" else "!UNITY_5_3_OR_NEWER"
        code = {name: ("#if " + condition + "\n").encode() + content + b"\n#endif\n" for name, content in code.items()}
        if output is not None:
            outputs[output.resolve()] = code
    if not check:
        publish(outputs)
    return tables


def main():
    root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description="校验 Excel 配置并生成二进制及 C# 数据类")
    parser.add_argument("--source-root", type=Path, default=Path("config/Excels"))
    parser.add_argument("--client-output", type=Path, default=Path("client/Res/Config"))
    parser.add_argument("--server-output", type=Path, default=Path("server/Res/Config"))
    parser.add_argument("--client-code", type=Path, default=Path("shared/Runtime/Gen/Client/Config"))
    parser.add_argument("--server-code", type=Path, default=Path("shared/Runtime/Gen/Server/Config"))
    parser.add_argument("--enums", type=Path, default=Path("tools/ConfigExporter/enums.json"))
    parser.add_argument("--check", action="store_true", help="仅校验，不写入文件")
    args = parser.parse_args()
    def relative(value):
        return value if value.is_absolute() else root / value
    try:
        tables = run(relative(args.source_root), relative(args.client_output), relative(args.server_output), relative(args.enums), args.check,
                     relative(args.client_code), relative(args.server_code))
        for table in tables:
            print(f"{'校验通过' if args.check else '已导出'}：{table.scope}/{table.name}，{len(table.rows)} 行，{len(table.fields)} 字段")
        return 0
    except (ConfigError, OSError, ValueError, struct.error) as exc:
        print(f"导表失败：{exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
