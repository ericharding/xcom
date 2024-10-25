using BlackEdgeCommon.Utils.Database;
using BlackEdgeCommon.Utils.Persistence;
using CommunityToolkit.HighPerformance.Buffers;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Communication
{
    public class ProtoBufSerializer<T> : ISerializer<T>
    {
        public static ProtoBufSerializer<T> Instance { get; } = new ProtoBufSerializer<T>();

        private ProtoBufSerializer()
        { }

        public void Serialize(Stream stream, T record) => Serializer.Serialize(stream, record);
        public T Deserialize(Stream stream) => Serializer.Deserialize<T>(stream);
    }

    public static class SerializeViaProtoBuf
    {
        public static byte[] SerializeProtoBuf<T>(this T message)
        {
            byte[] result;
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, message);
                result = stream.ToArray();
            }
            return result;
        }

        public static int SerializeProtoBufIntoBuffer<T>(this T message, Memory<byte> buffer)
        {
            MemoryBufferWriter<byte> writer = new MemoryBufferWriter<byte>(buffer);
            Serializer.Serialize(writer, message);
            return writer.WrittenCount;
        }

        public static int SerializeProtoBufIntoBuffer<T>(this T message, byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            {
                Serializer.Serialize(stream, message);
                return (int)stream.Position;
            }
        }

        public static T DeserializeProtoBuf<T>(this byte[] bytes)
        {
            T result;
            using (var stream = new MemoryStream(bytes))
            {
                result = Serializer.Deserialize<T>(stream);
            }
            return result;
        }

        public static T DeserializeProtoBuf<T>(this ReadOnlySpan<byte> bytes)
            => Serializer.Deserialize<T>(bytes);

        public static T DeserializeProtoBuf<T>(this ReadOnlySpan<byte> bytes, T valueToFill)
            => Serializer.Deserialize(bytes, valueToFill);

        public static T DeserializeProtoBuf<T>(this MemoryStream stream, T valueToFill)
            => Serializer.Deserialize(stream, valueToFill);

        public static void SerializeProtoBufToFile<T>(this T message, string filePath)
        {
            using (FileStream stream = File.Open(filePath, FileMode.Create))
            {
                Serializer.Serialize(stream, message);
            }
        }

        public static T DeserializeProtoBufFromFile<T>(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                return Serializer.Deserialize<T>(stream);
            }
        }

        public static T MessageDeepClone<T>(this T message) => SerializeProtoBuf<T>(message).DeserializeProtoBuf<T>();

        public static T NewRecordTimeDeepClone<T>(this T message) where T : ITimeStampedRecord
        {
            T clone = message.MessageDeepClone();
            clone.RecordTime = DateTime.Now;

            return clone;
        }
    }

    public class LazyCloneHelper<T> where T : ITimeStampedRecord
    {
        public bool CloneCreated => _clone != null;
        public T Clone
        {
            get
            {
                if (_clone == null)
                    _clone = _source.NewRecordTimeDeepClone();

                return _clone;
            }
        }

        private T _source;
        private T _clone;

        public LazyCloneHelper()
        { }

        public void Init(T source)
        {
            _source = source;
            _clone = default;
        }
    }
}
