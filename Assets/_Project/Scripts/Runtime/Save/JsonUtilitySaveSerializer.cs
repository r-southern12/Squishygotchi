using Squishy.Simulation.Save;
using UnityEngine;

namespace Squishy.Runtime.Save
{
    public sealed class JsonUtilitySaveSerializer : ISaveSerializer
    {
        private readonly bool _pretty;

        public JsonUtilitySaveSerializer(bool pretty)
        {
            _pretty = pretty;
        }

        public string Serialize(SaveData data)
        {
            return JsonUtility.ToJson(data, _pretty);
        }

        public SaveData Deserialize(string text)
        {
            return JsonUtility.FromJson<SaveData>(text);
        }
    }
}
