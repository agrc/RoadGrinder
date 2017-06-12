using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESRI.ArcGIS.Geodatabase;

namespace RoadGrinder.commands
{
    public static class AddIndexToFieldCommand
    {
        public static void Execute(IFeatureClass featureClass, String indexName, String nameOfField)
        {
            // Ensure the feature class contains the specified field.
            int fieldIndex = featureClass.FindField(nameOfField);
            if (fieldIndex == -1)
            {
                throw new ArgumentException("The specified field does not exist in the feature class.");
            }

            // Get the specified field from the feature class.
            IFields featureClassFields = featureClass.Fields;
            IField field = featureClassFields.get_Field(fieldIndex);

            // Create a fields collection and add the specified field to it.
            IFields fields = new FieldsClass();
            IFieldsEdit fieldsEdit = (IFieldsEdit)fields;
            fieldsEdit.FieldCount_2 = 1;
            fieldsEdit.set_Field(0, field);

            // Create an index and cast to the IIndexEdit interface.
            IIndex index = new IndexClass();
            IIndexEdit indexEdit = (IIndexEdit)index;

            // Set the index's properties, including the associated fields.
            indexEdit.Fields_2 = fields;
            indexEdit.IsAscending_2 = false;
            indexEdit.IsUnique_2 = false;
            indexEdit.Name_2 = indexName;

            // Add the index to the feature class.
            featureClass.AddIndex(index);
        }
    }
}
