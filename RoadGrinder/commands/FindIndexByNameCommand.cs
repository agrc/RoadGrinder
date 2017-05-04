using System.Collections.Generic;
using ESRI.ArcGIS.Geodatabase;
using RoadGrinder.models;

namespace RoadGrinder.commands
{
    public class FindIndexByNameCommand
    {
        private readonly IFields _fields;
        private readonly IEnumerable<string> _fieldsToMap;
        private readonly Dictionary<string, IndexFieldMap> _propertyValueIndexMap;

        /// <summary>
        ///     Initializes a new instance of the <see cref="FindIndexByNameCommand" /> class.
        /// </summary>
        /// <param name="layer"> The layer. </param>
        /// <param name="fieldsToMap">The fields to map to an index number</param>
        public FindIndexByNameCommand(IFeatureClass layer, IEnumerable<string> fieldsToMap)
        {
            _fieldsToMap = fieldsToMap;
            _fields = layer.Fields;
            _propertyValueIndexMap = new Dictionary<string, IndexFieldMap>();
        }

        /// <summary>
        ///     code to execute when command is run. Iterates over string in fields and finds the index for the field in the
        ///     feature class
        /// </summary>
        public Dictionary<string, IndexFieldMap> Execute()
        {
            foreach (var field in _fieldsToMap)
            {
                _propertyValueIndexMap.Add(field,
                    new IndexFieldMap(GetIndexForField(field, _fields), field));
            }

            return _propertyValueIndexMap;
        }

        /// <summary>
        ///     Gets the index for field.
        /// </summary>
        /// <param name="attributeName"> The attribute name. </param>
        /// <param name="fields"> The fields. </param>
        /// <returns> </returns>
        private static int GetIndexForField(string attributeName, IFields fields)
        {
            var findField = fields.FindField(attributeName.Trim());

            return findField < 0 ? fields.FindFieldByAliasName(attributeName.Trim()) : findField;
        }
    }
}