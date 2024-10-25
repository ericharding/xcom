using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlackEdgeCommon.Utils.Persistence
{
    public interface ISerializer<T>
    {
        void Serialize(Stream stream, T record);
        T Deserialize(Stream stream);
    }

    public class JsonSerializer<T> : ISerializer<T>
    {
        public static JsonSerializer<T> Instance { get; } = new JsonSerializer<T>();
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions { WriteIndented = true };

        private JsonSerializer()
        { }

        public void Serialize(Stream stream, T record) => JsonSerializer.Serialize(stream, record, _options);
        public T Deserialize(Stream stream) => JsonSerializer.Deserialize<T>(stream);
    }
}
