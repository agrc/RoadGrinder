using System;
using System.Collections.Generic;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using RoadGrinder.models;

namespace RoadGrinder.services
{
    internal class EsriHelper
    {
        //connect to sde - method

        #region "Connect to SDE"

        public static IWorkspace ConnectToTransactionalVersion(string server, string instance, string database, string authenication, string version)
        {
            IPropertySet propertySet = new PropertySetClass();
            propertySet.SetProperty("SERVER", server);
            //propertySet.SetProperty("DBCLIENT", dbclient);
            propertySet.SetProperty("INSTANCE", instance);
            propertySet.SetProperty("DATABASE", database);
            propertySet.SetProperty("AUTHENTICATION_MODE", authenication);
            propertySet.SetProperty("VERSION", version);

            var factoryType = Type.GetTypeFromProgID("esriDataSourcesGDB.SdeWorkspaceFactory");
            var workspaceFactory = (IWorkspaceFactory) Activator.CreateInstance(factoryType);
            return workspaceFactory.Open(propertySet, 0);
        }

        #endregion

        //connect to sde - method (this method has the same name so we can use method overloading)

        #region "Connect to SDE"

        public static IWorkspace ConnectToTransactionalVersion(string server, string instance, string database, string authenication, string version,
            string username, string pass)
        {
            IPropertySet propertySet = new PropertySetClass();
            propertySet.SetProperty("SERVER", server);
            //propertySet.SetProperty("DBCLIENT", dbclient);
            propertySet.SetProperty("INSTANCE", instance);
            propertySet.SetProperty("DATABASE", database);
            propertySet.SetProperty("AUTHENTICATION_MODE", authenication);
            propertySet.SetProperty("VERSION", version);
            propertySet.SetProperty("USER", username);
            propertySet.SetProperty("PASSWORD", pass);

            var factoryType = Type.GetTypeFromProgID("esriDataSourcesGDB.SdeWorkspaceFactory");
            var workspaceFactory = (IWorkspaceFactory) Activator.CreateInstance(factoryType);
            return workspaceFactory.Open(propertySet, 0);
        }

        #endregion

        // create a file geodatabase in user-specified location

        #region "Create FileGeodatabase"

        public static IWorkspace CreateFileGdbWorkspace(string strFgdPath, string strFgdName)
        {
            IWorkspaceName workspaceName = null;
            // Instantiate a file geodatabase workspace factory and create a new file geodatabase.
            // The Create method returns a workspace name object.
            IWorkspaceFactory workspaceFactory = new FileGDBWorkspaceFactoryClass();

            // check if file geodatabase exists, before creating it
            if (!(workspaceFactory.IsWorkspace(strFgdPath + strFgdName)))
            {
                workspaceName = workspaceFactory.Create(strFgdPath, strFgdName, null, 0);
            }
            else
            {
                IFileNames arcFileNames = new FileNames();
                arcFileNames.Add(strFgdPath + strFgdName);
                workspaceName = workspaceFactory.GetWorkspaceName(strFgdPath, arcFileNames);
            }

            // Cast the workspace name object to the IName interface and open the workspace.
            var name = (IName) workspaceName;
            var workspace = (IWorkspace) name.Open();
            return workspace;
        }

        #endregion

        // create feature class in file geodatabase

        #region "create feature class in file geodatabase"

        public static IFeatureClass CreateFeatureClass(string featureClassName, UID classExtensionUID, IFeatureWorkspace featureWorkspace)
        {
            // check if the fc exist, if so rename it


            // Create a fields collection for the feature class.
            IFields fields = new FieldsClass();
            var fieldsEdit = (IFieldsEdit) fields;

            // Add an object ID field to the fields collection. This is mandatory for feature classes.
            IField oidField = new FieldClass();
            var oidFieldEdit = (IFieldEdit) oidField;
            oidFieldEdit.Name_2 = "OBJECTID";
            oidFieldEdit.Type_2 = esriFieldType.esriFieldTypeOID;
            fieldsEdit.AddField(oidField);

            // Create a geometry definition (and spatial reference) for the feature class.
            IGeometryDef geometryDef = new GeometryDefClass();
            var geometryDefEdit = (IGeometryDefEdit) geometryDef;
            geometryDefEdit.GeometryType_2 = esriGeometryType.esriGeometryPolyline;
            ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass();
            ISpatialReference spatialReference = spatialReferenceFactory.CreateProjectedCoordinateSystem((int) esriSRProjCSType.esriSRProjCS_NAD1983UTM_12N);
            var spatialReferenceResolution = (ISpatialReferenceResolution) spatialReference;
            spatialReferenceResolution.ConstructFromHorizon();
            var spatialReferenceTolerance = (ISpatialReferenceTolerance) spatialReference;
            spatialReferenceTolerance.SetDefaultXYTolerance();
            geometryDefEdit.SpatialReference_2 = spatialReference;

            // Add a geometry field to the fields collection. This is where the geometry definition is applied.
            IField geometryField = new FieldClass();
            var geometryFieldEdit = (IFieldEdit) geometryField;
            geometryFieldEdit.Name_2 = "Shape";
            geometryFieldEdit.Type_2 = esriFieldType.esriFieldTypeGeometry;
            geometryFieldEdit.GeometryDef_2 = geometryDef;
            fieldsEdit.AddField(geometryField);

            // Create a text field called "ADDRSYS_L" for the fields collection.
            IField addrSysLField = new FieldClass();
            var addrSysLFieldEdit = (IFieldEdit) addrSysLField;
            addrSysLFieldEdit.Name_2 = "ADDRSYS_L";
            addrSysLFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            addrSysLFieldEdit.Length_2 = 30;
            fieldsEdit.AddField(addrSysLField);

            // Create a text field called "ADDRSYS_R" for the fields collection.
            IField addrSysRField = new FieldClass();
            var addrSysRFieldEdit = (IFieldEdit) addrSysRField;
            addrSysRFieldEdit.Name_2 = "ADDRSYS_R";
            addrSysRFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            addrSysRFieldEdit.Length_2 = 30;
            fieldsEdit.AddField(addrSysRField);


            // Create a text field called "FROMADDR_L" for the fields collection.
            IField rangeL_Ffield = new FieldClass();
            var rangeL_FfieldEdit = (IFieldEdit) rangeL_Ffield;
            rangeL_FfieldEdit.Name_2 = "FROMADDR_L";
            rangeL_FfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeL_FfieldEdit.Precision_2 = 38;
            rangeL_FfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeL_Ffield);

            // Create a text field called "TOADDR_L" for the fields collection.
            IField rangeL_Tfield = new FieldClass();
            var rangeL_TfieldEdit = (IFieldEdit) rangeL_Tfield;
            rangeL_TfieldEdit.Name_2 = "TOADDR_L";
            rangeL_TfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeL_TfieldEdit.Precision_2 = 38;
            rangeL_TfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeL_Tfield);

            // Create a text field called "FROMADDR_R" for the fields collection.
            IField rangeR_Ffield = new FieldClass();
            var rangeR_FfieldEdit = (IFieldEdit) rangeR_Ffield;
            rangeR_FfieldEdit.Name_2 = "FROMADDR_R";
            rangeR_FfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeR_FfieldEdit.Precision_2 = 38;
            rangeR_FfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeR_Ffield);

            // Create a text field called "TOADDR_R" for the fields collection.
            IField rangeR_Tfield = new FieldClass();
            var rangeR_TfieldEdit = (IFieldEdit) rangeR_Tfield;
            rangeR_TfieldEdit.Name_2 = "TOADDR_R";
            rangeR_TfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeR_TfieldEdit.Precision_2 = 38;
            rangeR_TfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeR_Tfield);

            // Create a text field called "PREDIR" for the fields collection.
            IField predirField = new FieldClass();
            var predirFieldEdit = (IFieldEdit) predirField;
            predirFieldEdit.Name_2 = "PREDIR";
            predirFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            predirFieldEdit.Length_2 = 1;
            fieldsEdit.AddField(predirField);

            // Create a text field called "NAME" for the fields collection.
            IField nameField = new FieldClass();
            var nameFieldEdit = (IFieldEdit) nameField;
            nameFieldEdit.Name_2 = "NAME";
            nameFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            nameFieldEdit.Length_2 = 30;
            fieldsEdit.AddField(nameField);

            // Create a text field called "POSTTYPE" for the fields collection.
            IField streettypeField = new FieldClass();
            var streettypeFieldEdit = (IFieldEdit) streettypeField;
            streettypeFieldEdit.Name_2 = "POSTTYPE";
            streettypeFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            streettypeFieldEdit.Length_2 = 4;
            fieldsEdit.AddField(streettypeField);

            // Create a text field called "POSTDIR" for the fields collection.
            IField sufdirField = new FieldClass();
            var sufdirFieldEdit = (IFieldEdit) sufdirField;
            sufdirFieldEdit.Name_2 = "POSTDIR";
            sufdirFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            sufdirFieldEdit.Length_2 = 2;
            fieldsEdit.AddField(sufdirField);

            // Create a text field called "ZIPCODE_L" for the fields collection.
            IField zipleftField = new FieldClass();
            var zipleftFieldEdit = (IFieldEdit) zipleftField;
            zipleftFieldEdit.Name_2 = "ZIPCODE_L";
            zipleftFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            zipleftFieldEdit.Length_2 = 5;
            fieldsEdit.AddField(zipleftField);

            // Create a text field called "ZIPCODE_R" for the fields collection.
            IField ziprightField = new FieldClass();
            var ziprightFieldEdit = (IFieldEdit) ziprightField;
            ziprightFieldEdit.Name_2 = "ZIPCODE_R";
            ziprightFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            ziprightFieldEdit.Length_2 = 5;
            fieldsEdit.AddField(ziprightField);

            // Create a text field called "GLOBALID_SGID" for the fields collection - to join to the table or sgid.
            IField globalidField = new FieldClass();
            var globalidFieldEdit = (IFieldEdit) globalidField;
            globalidFieldEdit.Name_2 = "GLOBALID_SGID";
            globalidFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                // use string and not the globalid type b/c it might that might assign it's own unique global id and this is for joinging back to sgid
            globalidFieldEdit.Length_2 = 50;
            fieldsEdit.AddField(globalidField);

            // Use IFieldChecker to create a validated fields collection.
            IFieldChecker fieldChecker = new FieldCheckerClass();
            IEnumFieldError enumFieldError = null;
            IFields validatedFields = null;
            fieldChecker.ValidateWorkspace = (IWorkspace) featureWorkspace;
            fieldChecker.Validate(fields, out enumFieldError, out validatedFields);

            // The enumFieldError enumerator can be inspected at this point to determine
            // which fields were modified during validation.

            // Create the feature class. Note that the CLSID parameter is null - this indicates to use the
            // default CLSID, esriGeodatabase.Feature (acceptable in most cases for feature classes).
            var featureClass = featureWorkspace.CreateFeatureClass(featureClassName, validatedFields, null, classExtensionUID, esriFeatureType.esriFTSimple,
                "Shape", "");

            return featureClass;
        }

        #endregion

        // create table in file geodatabase

        #region "create table in file geodatabase"

        public static ITable CreateTable(string tableName, UID classExtensionUID, IFeatureWorkspace featureWorkspace)
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

            // Create a text field called "ADDRSYS_L" for the fields collection.
            IField addrSysLField = new FieldClass();
            var addrSysLFieldEdit = (IFieldEdit) addrSysLField;
            addrSysLFieldEdit.Name_2 = "ADDRSYS_L";
            addrSysLFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            addrSysLFieldEdit.Length_2 = 30;
            fieldsEdit.AddField(addrSysLField);

            // Create a text field called "ADDRSYS_R" for the fields collection.
            IField addrSysRField = new FieldClass();
            var addrSysRFieldEdit = (IFieldEdit) addrSysRField;
            addrSysRFieldEdit.Name_2 = "ADDRSYS_R";
            addrSysRFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            addrSysRFieldEdit.Length_2 = 30;
            fieldsEdit.AddField(addrSysRField);

            // Create a text field called "FROMADDR_L" for the fields collection.
            IField rangeL_Ffield = new FieldClass();
            var rangeL_FfieldEdit = (IFieldEdit) rangeL_Ffield;
            rangeL_FfieldEdit.Name_2 = "FROMADDR_L";
            rangeL_FfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeL_FfieldEdit.Precision_2 = 38;
            rangeL_FfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeL_Ffield);

            // Create a text field called "TOADDR_L" for the fields collection.
            IField rangeL_Tfield = new FieldClass();
            var rangeL_TfieldEdit = (IFieldEdit) rangeL_Tfield;
            rangeL_TfieldEdit.Name_2 = "TOADDR_L";
            rangeL_TfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeL_TfieldEdit.Precision_2 = 38;
            rangeL_TfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeL_Tfield);

            // Create a text field called "FROMADDR_R" for the fields collection.
            IField rangeR_Ffield = new FieldClass();
            var rangeR_FfieldEdit = (IFieldEdit) rangeR_Ffield;
            rangeR_FfieldEdit.Name_2 = "FROMADDR_R";
            rangeR_FfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeR_FfieldEdit.Precision_2 = 38;
            rangeR_FfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeR_Ffield);

            // Create a text field called "TOADDR_R" for the fields collection.
            IField rangeR_Tfield = new FieldClass();
            var rangeR_TfieldEdit = (IFieldEdit) rangeR_Tfield;
            rangeR_TfieldEdit.Name_2 = "TOADDR_R";
            rangeR_TfieldEdit.Type_2 = esriFieldType.esriFieldTypeDouble;
            rangeR_TfieldEdit.Precision_2 = 38;
            rangeR_TfieldEdit.Scale_2 = 8;
            fieldsEdit.AddField(rangeR_Tfield);

            // Create a text field called "PREDIR" for the fields collection.
            IField predirField = new FieldClass();
            var predirFieldEdit = (IFieldEdit) predirField;
            predirFieldEdit.Name_2 = "PREDIR";
            predirFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            predirFieldEdit.Length_2 = 1;
            fieldsEdit.AddField(predirField);

            // Create a text field called "NAME" for the fields collection.
            IField nameField = new FieldClass();
            var nameFieldEdit = (IFieldEdit) nameField;
            nameFieldEdit.Name_2 = "NAME";
            nameFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            nameFieldEdit.Length_2 = 30;
            fieldsEdit.AddField(nameField);

            // Create a text field called "POSTTYPE" for the fields collection.
            IField streettypeField = new FieldClass();
            var streettypeFieldEdit = (IFieldEdit) streettypeField;
            streettypeFieldEdit.Name_2 = "POSTTYPE";
            streettypeFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            streettypeFieldEdit.Length_2 = 4;
            fieldsEdit.AddField(streettypeField);

            // Create a text field called "POSTDIR" for the fields collection.
            IField sufdirField = new FieldClass();
            var sufdirFieldEdit = (IFieldEdit) sufdirField;
            sufdirFieldEdit.Name_2 = "POSTDIR";
            sufdirFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            sufdirFieldEdit.Length_2 = 2;
            fieldsEdit.AddField(sufdirField);

            // Create a text field called "ZIPCODE_L" for the fields collection.
            IField zipleftField = new FieldClass();
            var zipleftFieldEdit = (IFieldEdit) zipleftField;
            zipleftFieldEdit.Name_2 = "ZIPCODE_L";
            zipleftFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            zipleftFieldEdit.Length_2 = 5;
            fieldsEdit.AddField(zipleftField);

            // Create a text field called "ZIPCODE_R" for the fields collection.
            IField ziprightField = new FieldClass();
            var ziprightFieldEdit = (IFieldEdit) ziprightField;
            ziprightFieldEdit.Name_2 = "ZIPCODE_R";
            ziprightFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
            ziprightFieldEdit.Length_2 = 5;
            fieldsEdit.AddField(ziprightField);

            // Create a text field called "GLOBALID_SGID" for the fields collection - to join back to the feature class or sgid.
            IField globalidField = new FieldClass();
            var globalidFieldEdit = (IFieldEdit) globalidField;
            globalidFieldEdit.Name_2 = "GLOBALID_SGID";
            globalidFieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                // use string and not the globalid type b/c it might that might assign it's own unique global id and this is for joinging back to sgid
            globalidFieldEdit.Length_2 = 50;
            fieldsEdit.AddField(globalidField);

            // Use IFieldChecker to create a validated fields collection.
            IFieldChecker fieldChecker = new FieldCheckerClass();
            IEnumFieldError enumFieldError = null;
            IFields validatedFields = null;
            fieldChecker.ValidateWorkspace = (IWorkspace) featureWorkspace;
            fieldChecker.Validate(fields, out enumFieldError, out validatedFields);

            // Create the feature class. Note that the CLSID parameter is null - this indicates to use the
            // default CLSID, esriGeodatabase.Feature (acceptable in most cases for feature classes).
            var arcTable = featureWorkspace.CreateTable(tableName, validatedFields, null, classExtensionUID, "");

            return arcTable;
        }

        #endregion

        // check if data exists in file geodatabase

        #region "check if name exists in database"

        // this method checks if the feature class or table exist in the geodatabase
        public static bool NameExists(IWorkspace2 workspace, string strFCName, esriDatasetType dataType)
        {
            var blnNameExists = workspace.get_NameExists(dataType, strFCName);
            return blnNameExists;
        }

        #endregion

        // insert new row/record in the geocode file geodatabase

        #region "insert row into geodatabase"

        public static void InsertFeatureInto(IFeature feature, IFeatureClass fc, Dictionary<string, IndexFieldValue> fieldValues)
        {
            try
            {
                var newFeature = fc.CreateFeature();
                var shape = newFeature.ShapeCopy;

                var simpleShape = (IFeatureSimplify) shape;
                simpleShape.SimplifyGeometry(shape);

                newFeature.Shape = shape;

                foreach (var fieldValue in fieldValues)
                {
                    newFeature.set_Value(fieldValue.Value.Index, fieldValue.Value);
                }

                newFeature.Store();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "There was an error with InsertFeatureInto method." +
                    ex.Message + " " + ex.Source + " " + ex.InnerException + " " + ex.HResult + " " + ex.StackTrace + " " + ex);
                Console.ReadLine();
            }
        }
        #endregion
    }
}
