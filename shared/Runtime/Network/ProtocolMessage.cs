using System.IO;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace GameSync.Network
{
    /// <summary>读取消息体协议号并调用对应的 Protobuf 解析器。</summary>
    public static class ProtocolMessage
    {
        /// <summary>读取消息首字段中的整数协议号，供接收端选择专属解析器。</summary>
        /// <param name="bytes">完整 Protobuf 消息字节。</param>
        /// <returns>消息体声明的整数协议号。</returns>
        public static uint ReadProtocolId(byte[] bytes)
        {
            using (CodedInputStream input = new CodedInputStream(bytes))
            {
                while (!input.IsAtEnd)
                {
                    uint tag = input.ReadTag();
                    if (WireFormat.GetTagFieldNumber(tag) == 1)
                        return input.ReadUInt32();
                    input.SkipLastField();
                }
            }

            throw new InvalidDataException("消息缺少协议号字段。");
        }

        /// <summary>序列化已由生成构造逻辑写入固定协议号的消息本体。</summary>
        /// <param name="message">独立请求、响应或通知消息。</param>
        /// <returns>消息本体序列化字节，不添加额外网络封套。</returns>
        public static byte[] Encode(IMessage message)
        {
            return message.ToByteArray();
        }

        /// <summary>读取由生成代码固定的消息协议号。</summary>
        /// <param name="message">独立请求、响应或通知消息。</param>
        /// <returns>消息构造时由生成代码写入的整数协议号。</returns>
        public static uint GetProtocolId(IMessage message)
        {
            FieldDescriptor protocolField = message.Descriptor.FindFieldByNumber(1);
            if (protocolField == null || !(protocolField.Accessor.GetValue(message) is uint protocolId) || protocolId == 0)
                throw new InvalidDataException("消息类型未配置协议号。");

            return protocolId;
        }

        /// <summary>按指定协议号解析专属消息类型，并验证消息体中的协议号。</summary>
        /// <typeparam name="TMessage">协议号对应的 Protobuf 消息类型。</typeparam>
        /// <param name="bytes">完整 Protobuf 消息字节。</param>
        /// <param name="expectedProtocolId">调用方允许的整数协议号。</param>
        /// <param name="parser">专属消息解析器。</param>
        /// <returns>已通过协议号校验的消息对象。</returns>
        public static TMessage Decode<TMessage>(byte[] bytes, uint expectedProtocolId, MessageParser<TMessage> parser)
            where TMessage : IMessage<TMessage>
        {
            TMessage message = parser.ParseFrom(bytes);
            if (ReadProtocolId(bytes) != expectedProtocolId)
                throw new InvalidDataException("协议号与消息类型不一致。");
            return message;
        }
    }
}
