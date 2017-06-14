using ESRI.ArcGIS.Geodatabase;

namespace RoadGrinder.commands
{
    public static class CreateFieldsCollectionRoadsCommand
    {
        public static IFields Execute()
        {
            // Create a fields collection for the feature class.
            IFields fields = new FieldsClass();
            var fieldsEdit = (IFieldsEdit)fields;

            // Add an object ID field to the fields collection. This is mandatory for feature classes.
            IField oidField = new FieldClass();
            var oidFieldEdit = (IFieldEdit)oidField;
            oidFieldEdit.Name_2 = "OBJECTID";
            oidFieldEdit.Type_2 = esriFieldType.esriFieldTypeOID;
            fieldsEdit.AddField(oidField);

            // Create a text field called "ADDRSYS_L" for the fields collection.
            IField addrSysLField = new FieldClass();
            var addrSysLFieldEdit = (IFieldEdit)addrSysLField;
            addrSysLFieldEdit.Name_2 = "ADDRSYS_L";
            addrSysLFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            addrSysLFieldEdit.Length_2 = 30;
            fieldsEdit.AddField(addrSysLField);

            // Create a text field called "ADDRSYS_R" for the fields collection.
            IField addrSysRField = new FieldClass();
            var addrSysRFieldEdit = (IFieldEdit)addrSysRField;
            addrSysRFieldEdit.Name_2 = "ADDRSYS_R";
            addrSysRFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            addrSysRFieldEdit.Length_2 = 30;
            fieldsEdit.AddField(addrSysRField);

            // Create a text field called "FROMADDR_L" for the fields collection.
            IField rangeL_Ffield = new FieldClass();
            var rangeL_FfieldEdit = (IFieldEdit)rangeL_Ffield;
            rangeL_FfieldEdit.Name_2 = "FROMADDR_L";
            rangeL_FfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeL_FfieldEdit.Precision_2 = 38;
            rangeL_FfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeL_Ffield);

            // Create a text field called "TOADDR_L" for the fields collection.
            IField rangeL_Tfield = new FieldClass();
            var rangeL_TfieldEdit = (IFieldEdit)rangeL_Tfield;
            rangeL_TfieldEdit.Name_2 = "TOADDR_L";
            rangeL_TfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeL_TfieldEdit.Precision_2 = 38;
            rangeL_TfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeL_Tfield);

            // Create a text field called "FROMADDR_R" for the fields collection.
            IField rangeR_Ffield = new FieldClass();
            var rangeR_FfieldEdit = (IFieldEdit)rangeR_Ffield;
            rangeR_FfieldEdit.Name_2 = "FROMADDR_R";
            rangeR_FfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeR_FfieldEdit.Precision_2 = 38;
            rangeR_FfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeR_Ffield);

            // Create a text field called "TOADDR_R" for the fields collection.
            IField rangeR_Tfield = new FieldClass();
            var rangeR_TfieldEdit = (IFieldEdit)rangeR_Tfield;
            rangeR_TfieldEdit.Name_2 = "TOADDR_R";
            rangeR_TfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeR_TfieldEdit.Precision_2 = 38;
            rangeR_TfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeR_Tfield);

            // Create a text field called "PREDIR" for the fields collection.
            IField predirField = new FieldClass();
            var predirFieldEdit = (IFieldEdit)predirField;
            predirFieldEdit.Name_2 = "PREDIR";
            predirFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            predirFieldEdit.IsNullable_2 = false;
            predirFieldEdit.Length_2 = 1;
            fieldsEdit.AddField(predirField);

            // Create a text field called "NAME" for the fields collection.
            IField nameField = new FieldClass();
            var nameFieldEdit = (IFieldEdit)nameField;
            nameFieldEdit.Name_2 = "NAME";
            nameFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            nameFieldEdit.IsNullable_2 = false;
            nameFieldEdit.Length_2 = 30;
            fieldsEdit.AddField(nameField);

            // Create a text field called "POSTTYPE" for the fields collection.
            IField streettypeField = new FieldClass();
            var streettypeFieldEdit = (IFieldEdit)streettypeField;
            streettypeFieldEdit.Name_2 = "POSTTYPE";
            streettypeFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            streettypeFieldEdit.IsNullable_2 = false;
            streettypeFieldEdit.Length_2 = 4;
            fieldsEdit.AddField(streettypeField);

            // Create a text field called "POSTDIR" for the fields collection.
            IField sufdirField = new FieldClass();
            var sufdirFieldEdit = (IFieldEdit)sufdirField;
            sufdirFieldEdit.Name_2 = "POSTDIR";
            sufdirFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            sufdirFieldEdit.Length_2 = 2;
            sufdirFieldEdit.IsNullable_2 = false;
            fieldsEdit.AddField(sufdirField);

            // Create a text field called "ZIPCODE_L" for the fields collection.
            IField zipleftField = new FieldClass();
            var zipleftFieldEdit = (IFieldEdit)zipleftField;
            zipleftFieldEdit.Name_2 = "ZIPCODE_L";
            zipleftFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            zipleftFieldEdit.Length_2 = 5;
            fieldsEdit.AddField(zipleftField);

            // Create a text field called "ZIPCODE_R" for the fields collection.
            IField ziprightField = new FieldClass();
            var ziprightFieldEdit = (IFieldEdit)ziprightField;
            ziprightFieldEdit.Name_2 = "ZIPCODE_R";
            ziprightFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            ziprightFieldEdit.Length_2 = 5;
            fieldsEdit.AddField(ziprightField);

            // Create a text field called "GLOBALID_SGID" for the fields collection - to join back to the feature class or sgid.
            IField globalidField = new FieldClass();
            var globalidFieldEdit = (IFieldEdit)globalidField;
            globalidFieldEdit.Name_2 = "GLOBALID_SGID";
            globalidFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            // use string and not the globalid type b/c it might that might assign it's own unique global id and this is for joinging back to sgid
            globalidFieldEdit.Length_2 = 50;
            fieldsEdit.AddField(globalidField);

            return fields;
        }
    }
}
