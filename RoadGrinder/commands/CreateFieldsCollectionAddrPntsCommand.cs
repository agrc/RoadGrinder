using ESRI.ArcGIS.Geodatabase;

namespace RoadGrinder.commands
{
    public static class CreateFieldsCollectionAddrPntsCommand
    {
        public static IFields Execute()
        {
            // Create a fields collection for the feature class.
            IFields fields = new FieldsClass();
            var fieldsEdit = (IFieldsEdit) fields;

            // Add an object ID field to the fields collection. This is mandatory for feature classes.
            IField oidField = new FieldClass();
            var oidFieldEdit = (IFieldEdit) oidField;
            oidFieldEdit.Name_2 = "OBJECTID";
            oidFieldEdit.Type_2 = esriFieldType.esriFieldTypeOID;
            fieldsEdit.AddField(oidField);

            // Create a text field called "AddSystem" for the fields collection.
            IField addrSysField = new FieldClass();
            var addrSysFieldEdit = (IFieldEdit) addrSysField;
            addrSysFieldEdit.Name_2 = "AddSystem";
            addrSysFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            addrSysFieldEdit.Length_2 = 40;
            fieldsEdit.AddField(addrSysField);

            // Create a text field called "AddNum" for the fields collection.
            IField addNumField = new FieldClass();
            var addNumFieldEdit = (IFieldEdit) addNumField;
            addNumFieldEdit.Name_2 = "AddNum";
            addNumFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            addNumFieldEdit.Length_2 = 10;
            fieldsEdit.AddField(addNumField);

            // Create a text field called "AddNumSuffix" for the fields collection.
            IField addNumSuffixField = new FieldClass();
            var addNumSuffixFieldEdit = (IFieldEdit)addNumSuffixField;
            addNumSuffixFieldEdit.Name_2 = "AddNumSuffix";
            addNumSuffixFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            addNumSuffixFieldEdit.Length_2 = 4;
            fieldsEdit.AddField(addNumSuffixField);

            // Create a text field called "PrefixDir" for the fields collection.
            IField predirField = new FieldClass();
            var predirFieldEdit = (IFieldEdit) predirField;
            predirFieldEdit.Name_2 = "PrefixDir";
            predirFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            predirFieldEdit.IsNullable_2 = false;
            predirFieldEdit.Length_2 = 1;
            fieldsEdit.AddField(predirField);

            // Create a text field called "StreetName" for the fields collection.
            IField streetNameField = new FieldClass();
            var streetNameFieldEdit = (IFieldEdit) streetNameField;
            streetNameFieldEdit.Name_2 = "StreetName";
            streetNameFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            streetNameFieldEdit.Length_2 = 50;
            fieldsEdit.AddField(streetNameField);

            // Create a text field called "StreetType" for the fields collection.
            IField streettypeField = new FieldClass();
            var streettypeFieldEdit = (IFieldEdit) streettypeField;
            streettypeFieldEdit.Name_2 = "StreetType";
            streettypeFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            streettypeFieldEdit.Length_2 = 4;
            streettypeFieldEdit.IsNullable_2 = false;
            fieldsEdit.AddField(streettypeField);

            // Create a text field called "SuffixDir" for the fields collection.
            IField sufdirField = new FieldClass();
            var sufdirFieldEdit = (IFieldEdit) sufdirField;
            sufdirFieldEdit.Name_2 = "SuffixDir";
            sufdirFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            sufdirFieldEdit.Length_2 = 1;
            sufdirFieldEdit.IsNullable_2 = false;
            fieldsEdit.AddField(sufdirField);

            // Create a text field called "ZipCode" for the fields collection.
            IField zipField = new FieldClass();
            var zipFieldEdit = (IFieldEdit) zipField;
            zipFieldEdit.Name_2 = "ZipCode";
            zipFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            zipFieldEdit.Length_2 = 5;
            fieldsEdit.AddField(zipField);

            // Create a text field called "UnitType" for the fields collection.
            IField unitTypeField = new FieldClass();
            var unitTypeFieldEdit = (IFieldEdit)unitTypeField;
            unitTypeFieldEdit.Name_2 = "UnitType";
            unitTypeFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            unitTypeFieldEdit.Length_2 = 20;
            fieldsEdit.AddField(unitTypeField);

            // Create a text field called "UnitID" for the fields collection.
            IField unitIdField = new FieldClass();
            var unitIdFieldEdit = (IFieldEdit)unitIdField;
            unitIdFieldEdit.Name_2 = "UnitID";
            unitIdFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            unitIdFieldEdit.Length_2 = 20;
            fieldsEdit.AddField(unitIdField);

            // Create a text field called "City" for the fields collection.
            IField cityField = new FieldClass();
            var cityFieldEdit = (IFieldEdit)cityField;
            cityFieldEdit.Name_2 = "City";
            cityFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            cityFieldEdit.Length_2 = 30;
            fieldsEdit.AddField(cityField);

            // Create a text field called "CountyID" for the fields collection.
            IField countyIdField = new FieldClass();
            var countyIdFieldEdit = (IFieldEdit)countyIdField;
            countyIdFieldEdit.Name_2 = "CountyID";
            countyIdFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            countyIdFieldEdit.Length_2 = 15;
            fieldsEdit.AddField(countyIdField);

            // Create a text field called "UTAddPtID" for the fields collection - to join back to the feature class or sgid.
            IField uniqueIdField = new FieldClass();
            var uniqueIdFieldEdit = (IFieldEdit) uniqueIdField;
            uniqueIdFieldEdit.Name_2 = "UTAddPtID";
            uniqueIdFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            uniqueIdFieldEdit.Length_2 = 140;
            fieldsEdit.AddField(uniqueIdField);

            return fields;
        }




    }
}
