"""从公共 .proto 生成带固定协议号的 C# 消息。"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path


PROTOBUF_TOOLS_VERSION = "3.36.2"
PROTOCOL_FILE_NAME = "NetworkProtocol.proto"
PROTOCOL_ID_PATTERN = re.compile(
    r"(?m)^[ \t]*//[ \t]*protocol_id[ \t]*=[ \t]*(\d+)[ \t]*\r?\n"
    r"[ \t]*message[ \t]+([A-Za-z_][A-Za-z0-9_]*)[ \t]*\{"
)
MESSAGE_PATTERN = re.compile(
    r"(?s)(?P<header>message[ \t]+(?P<name>[A-Za-z_][A-Za-z0-9_]*)[ \t]*\{)"
    r"(?P<body>.*?)(?P<close>\})"
)
FIELD_PATTERN = re.compile(
    r"(?m)^(?P<declaration>[ \t]*(?:(?:repeated|required|optional)[ \t]+)?"
    r"[A-Za-z_][A-Za-z0-9_.]*[ \t]+[A-Za-z_][A-Za-z0-9_]*)"
    r"[ \t]*;(?P<suffix>[ \t]*(?://[^\r\n]*)?\r?)$"
)


def find_protoc() -> Path:
    """返回已还原的 Google.Protobuf.Tools 中的 Windows protoc。"""
    # 版本随 shared.csproj 锁定，防止生成器和运行库产生不同版本的代码。
    packages_root = Path(
        os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget" / "packages")
    )
    protoc = (
        packages_root
        / "google.protobuf.tools"
        / PROTOBUF_TOOLS_VERSION
        / "tools"
        / "windows_x64"
        / "protoc.exe"
    )
    if not protoc.is_file():
        raise FileNotFoundError(
            f"找不到 Google.Protobuf.Tools {PROTOBUF_TOOLS_VERSION} 的 protoc：{protoc}。"
            "请先执行 dotnet restore shared/shared.csproj。"
        )
    return protoc


def add_protocol_ids(source: str) -> tuple[str, list[str], list[str]]:
    """为注释标记的消息添加协议字段，并生成协议常量和构造默认值。"""
    protocol_ids: set[int] = set()
    constants: list[str] = []
    initializers: list[str] = []

    def replace_message(match: re.Match[str]) -> str:
        protocol_id = int(match.group(1))
        message_name = match.group(2)
        # 只检查消息自己的字段，避免协议号与手写字段冲突或重复分配。
        body_start = match.end()
        body_end = source.find("}", body_start)
        if body_end < 0:
            raise ValueError(f"{message_name} 消息缺少结束括号。")
        body = source[body_start:body_end]
        if re.search(r"\bprotocol_id\b", body):
            raise ValueError(f"{message_name} 不要手动声明 protocol_id，生成器会统一添加字段。")
        if not 0 < protocol_id <= 0xFFFFFFFF:
            raise ValueError(f"{message_name} 的协议号超出 uint32 范围：{protocol_id}")
        if protocol_id in protocol_ids:
            raise ValueError(f"协议号重复：{protocol_id}")
        protocol_ids.add(protocol_id)

        constants.append(f"        public const uint {message_name} = {protocol_id};")
        initializers.extend(
            [
                f"    public sealed partial class {message_name}",
                "    {",
                "        partial void OnConstruction()",
                "        {",
                f"            ProtocolId = ProtocolIds.{message_name};",
                "        }",
                "    }",
                "",
            ]
        )
        return match.group(0) + "\n  uint32 protocol_id;"

    generated = PROTOCOL_ID_PATTERN.sub(replace_message, source)
    if not constants:
        raise ValueError(f"{PROTOCOL_FILE_NAME} 没有配置任何 protocol_id 注释。")
    return generated, constants, initializers


def number_message_fields(source: str) -> str:
    """按字段声明顺序自动编号，并为响应追加通用错误码。"""

    def replace_message(match: re.Match[str]) -> str:
        message_name = match.group("name")
        body = match.group("body")
        if message_name.endswith("Response"):
            if re.search(r"\berror_code\b", body):
                raise ValueError(f"{message_name} 的 ErrorCode 字段由生成器添加，请从源文件删除。")
            protocol_id_field = re.compile(
                r"(?m)^(?P<indent>[ \t]*)uint32 protocol_id;(?P<ending>\r?\n)"
            )
            body, replacements = protocol_id_field.subn(
                lambda field: (
                    f"{field.group('indent')}uint32 protocol_id;{field.group('ending')}"
                    f"{field.group('indent')}ErrorCode error_code;\n"
                ),
                body,
                count=1,
            )
            if replacements != 1:
                raise ValueError(f"{message_name} 找不到生成器插入的 protocol_id 字段。")

        # 在统一编号前完成字段插入，协议号固定在 1，响应错误码固定在 2。
        if re.search(r"(?m)^\s*(?:oneof|reserved|extensions|option)\b|\bmap\s*<", body):
            raise ValueError("当前生成器不支持 oneof、reserved、扩展、消息选项或 map 字段。")
        if re.search(r"=\s*\d+\s*;", body):
            raise ValueError("NetworkProtocol.proto 字段编号由生成器填写，请删除字段后的 = 数字。")

        field_number = 0

        def number_field(field: re.Match[str]) -> str:
            nonlocal field_number
            field_number += 1
            return f"{field.group('declaration')} = {field_number};{field.group('suffix')}"

        numbered_body = FIELD_PATTERN.sub(number_field, body)
        if field_number == 0:
            raise ValueError(f"{message_name} 消息定义中没有可自动编号的字段。")
        return match.group("header") + numbered_body + match.group("close")

    return MESSAGE_PATTERN.sub(replace_message, source)


def write_generated_sources(output: Path, constants: list[str], initializers: list[str]) -> None:
    """写入协议常量和消息构造默认值。"""
    output.mkdir(parents=True, exist_ok=True)
    generated_header = (
        "// <auto-generated>\n"
        f"//     由 ProtocolGenerator/Generate.py 从 {PROTOCOL_FILE_NAME} 生成。\n"
        "// </auto-generated>\n"
    )
    ids_source = (
        generated_header
        + "namespace GameSync.Protocol.Generated\n{\n"
        + "    /// <summary>从 .proto 注释生成的固定整数协议号。</summary>\n"
        + "    public static class ProtocolIds\n    {\n"
        + "\n".join(constants)
        + "\n    }\n}\n"
    )
    defaults_source = (
        generated_header
        + "namespace GameSync.Protocol.Generated\n{\n"
        + "\n".join(initializers)
        + "}\n"
    )
    (output / "ProtocolIds.g.cs").write_text(ids_source, encoding="utf-8", newline="\n")
    (output / "ProtocolDefaults.g.cs").write_text(defaults_source, encoding="utf-8", newline="\n")


def main() -> int:
    """准备临时 proto 副本，调用 protoc 并更新项目生成代码。"""
    parser = argparse.ArgumentParser(description="生成 GameSync 公共 Protobuf 协议")
    parser.add_argument(
        "--repository-root",
        type=Path,
        default=Path(__file__).resolve().parents[2],
        help="仓库根目录，默认根据脚本位置自动查找",
    )
    args = parser.parse_args()
    repository_root = args.repository_root.resolve()
    protocol_directory = repository_root / "protocol" / "Common"
    generator_input = repository_root / ".build" / "ProtocolGenerator" / "Input"
    output = repository_root / "shared" / "Runtime" / "Gen" / "Protocol"

    try:
        # 先处理源文件，再用 protoc 生成消息类，最后写入项目自有的协议号辅助代码。
        protoc = find_protoc()
        source_path = protocol_directory / PROTOCOL_FILE_NAME
        source = source_path.read_text(encoding="utf-8-sig")
        generated_proto, constants, initializers = add_protocol_ids(source)
        generated_proto = number_message_fields(generated_proto)

        generator_input.mkdir(parents=True, exist_ok=True)
        probe_source = generator_input / "NetworkProbe.proto"
        protocol_source = generator_input / PROTOCOL_FILE_NAME
        probe_source.write_bytes((protocol_directory / "NetworkProbe.proto").read_bytes())
        protocol_source.write_text(generated_proto, encoding="utf-8", newline="\n")

        result = subprocess.run(
            [
                str(protoc),
                f"--proto_path={generator_input}",
                f"--proto_path={protocol_directory}",
                f"--csharp_out={output}",
                str(probe_source),
                str(protocol_source),
            ],
            check=False,
        )
        if result.returncode != 0:
            raise RuntimeError(f"protoc 生成失败，退出码：{result.returncode}")
        write_generated_sources(output, constants, initializers)
    except (OSError, ValueError, RuntimeError) as error:
        print(f"协议生成失败：{error}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
