using System.Collections.Generic;
using ESRI.ArcGIS.Geodatabase;
using RoadGrinder.models;

namespace RoadGrinder.commands
{
    public class AddValueToIndexFieldMapCommand
    {
        private readonly Dictionary<string, IndexFieldMap> _map;
        private readonly IFeature _feature;

        public AddValueToIndexFieldMapCommand(Dictionary<string, IndexFieldMap> map, IFeature feature)
        {
            _map = map;
            _feature = feature;
        }

        public Dictionary<string, IndexFieldValue> Execute()
        {
            var updatedDictionary = new Dictionary<string, IndexFieldValue>(_map.Count);

            foreach (var map in _map)
            {
                var index = map.Value.Index;
                var field = map.Value.Field;
                var attributeName = map.Key;

                updatedDictionary.Add(attributeName, new IndexFieldValue(index, field, GetValueForField(index)));
            }

            return updatedDictionary;
        }

        private object GetValueForField(int index)
        {
            return _feature.get_Value(index);
        }
    }
}