using System;
using System.Collections.Generic;
using ESRI.ArcGIS.ADF;
using ESRI.ArcGIS.Geodatabase;
using RoadGrinder.commands;
using RoadGrinder.contracts;
using RoadGrinder.models;
using RoadGrinder.services;

namespace RoadGrinder.grinders
{
    public class AlternateNamesGrinder : IGrindable
    {
        private const string GeocodeRoadsFeatureClassName = "GeocodeRoads";
        private const string GeocodeRoadsTableName = "GeocodeRoadAtlNames";
        private readonly CliOptions _options;
        private readonly IFeatureClass _roads;
        private IFeatureClass _geocodeRoads;

        public AlternateNamesGrinder(IFeatureClass source, CliOptions options)
        {
            _roads = source;
            _options = options;
        }

        public void Grind(IWorkspace output)
        {
            IWorkspaceEdit outputEditWorkspace = null;

            try
            {
                var outputFeatureWorkspace = (IFeatureWorkspace) output;

                // create a feature cursor from the source roads data and loop through this subset
                // create the query filter to filter results
                // FOR TESTING...                 
                const string streetsThatCrossGrids = @"ADDR_SYS = 'SALT LAKE CITY' and CARTOCODE not in ('1','7','99') and 
((L_F_ADD <> 0 and L_T_ADD <> 0) OR (R_F_ADD <> 0 and R_T_ADD <> 0)) and 
STREETNAME <> '' and STREETNAME not like '%ROUNDABOUT%'";

                // GO LIVE...
                //strQuery = @"CARTOCODE not in ('1','7','99') and ((L_F_ADD <> 0 and L_T_ADD <> 0) OR (R_F_ADD <> 0 and R_T_ADD <> 0)) and STREETNAME <> '' and STREETNAME not like '%ROUNDABOUT%'";
                var roadsFilter = new QueryFilter
                {
                    WhereClause = streetsThatCrossGrids
                };

                // create a ComReleaser for feature cursor's life-cycle management
                outputEditWorkspace = (IWorkspaceEdit) outputFeatureWorkspace;
                using (var comReleaser = new ComReleaser())
                {
                    var roadsCursor = _roads.Search(roadsFilter, false);
                    comReleaser.ManageLifetime(roadsCursor);

                    // begin an edit session on the file geodatabase (maybe) that way we can roll back if it errors out
                    outputEditWorkspace.StartEditing(false);
                    outputEditWorkspace.StartEditOperation();

                    IFeature roadFeature;
                    var fieldIndexMap = new FindIndexByNameCommand(_roads, new[]
                    {
                        "ADDR_SYS", "L_F_ADD", "L_T_ADD", "R_F_ADD", "R_T_ADD", "PREDIR",
                        "STREETNAME", "STREETTYPE", "SUFDIR", "ALIAS1", "ALIAS1TYPE", "ALIAS2",
                        "ALIAS2TYPE", "ACSNAME", "ACSSUF", "ZIPLEFT", "ZIPRIGHT", "GLOBALID"
                    }).Execute();

                    // loop through the sgid roads' feature cursor
                    while ((roadFeature = roadsCursor.NextFeature()) != null)
                    {
                        var valueMap = new AddValueToIndexFieldMapCommand(fieldIndexMap, roadFeature).Execute();

                        // begin to populate the geocode feature class in the newly-created file geodatabase
                        // check if this segment has a streetname
                        if (!string.IsNullOrEmpty(valueMap["STREETNAME"].Value.ToString()))
                        {
                            // only write necessary key/values
                            var streetValueMap = valueMap;
                            RemoveKeys(streetValueMap);

                            // create a new feature
                            EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, streetValueMap);
                        }
                        // check if this segment has an alias name
                        if (!string.IsNullOrEmpty(valueMap["ALIAS1"].Value.ToString()))
                        {
                            var aliasValueMap = valueMap;
                            aliasValueMap["STREETNAME"] = aliasValueMap["ALIAS1"];
                            aliasValueMap["STREETTYPE"] = aliasValueMap["ALIAS1TYPE"];

                            RemoveKeys(aliasValueMap);

                            // create a new feature
                            EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, aliasValueMap);
                        }
                        // check if this segment has a second alias name
                        if (!string.IsNullOrEmpty(valueMap["ALIAS2"].Value.ToString()))
                        {
                            var aliasValueMap = valueMap;
                            aliasValueMap["STREETNAME"] = aliasValueMap["ALIAS2"];
                            aliasValueMap["STREETTYPE"] = aliasValueMap["ALIAS2TYPE"];

                            RemoveKeys(aliasValueMap);

                            // create a new feature
                            EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, aliasValueMap);
                        }
                        // check if this segment has an acs alias name
                        if (!string.IsNullOrEmpty(valueMap["ACSNAME"].Value.ToString()))
                        {
                            var acsValueMap = valueMap;
                            acsValueMap["STREETNAME"] = acsValueMap["ACSNAME"];
                            acsValueMap["SUFDIR"] = acsValueMap["ACSSUF"];

                            acsValueMap.Remove("STREETTYPE");
                            RemoveKeys(acsValueMap);

                            // create a new feature
                            EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, acsValueMap);
                        }
                    }

                    outputEditWorkspace.StopEditOperation();
                    outputEditWorkspace.StopEditing(true);
                }
            }
            finally
            {
                // stop editing and don't save the edits
                if (outputEditWorkspace != null && outputEditWorkspace.IsBeingEdited())
                {
                    outputEditWorkspace.StopEditOperation();
                    outputEditWorkspace.StopEditing(false);
                }
            }
        }

        public IWorkspace CreateOutput()
        {
            // create the file geodatabase for the derived geocoding data
            var output = EsriHelper.CreateFileGdbWorkspace(_options.OutputGeodatabase, GeocodeRoadsFeatureClassName);
            // releaser.ManageLifetime(output);

            var outputFeatureWorkspace = (IFeatureWorkspace) output;
            var outputWorkspace2 = (IWorkspace2) output;

            // check if the feature class and table exist in the file geodatabase - if so rename them before adding new data
            if (EsriHelper.NameExists(outputWorkspace2, GeocodeRoadsFeatureClassName, esriDatasetType.esriDTFeatureClass))
            {
                // rename existing fc
                var renameFeatureClass = outputFeatureWorkspace.OpenFeatureClass(GeocodeRoadsFeatureClassName);
                // releaser.ManageLifetime(renameFeatureClass);

                var outputDataset = (IDataset) renameFeatureClass;
                outputDataset.Rename(string.Format("{0}{1}{2}", GeocodeRoadsFeatureClassName, "ReplacedOn", DateTime.Now.ToString("yyyyMMdd")));
            }

            if (EsriHelper.NameExists(outputWorkspace2, GeocodeRoadsTableName, esriDatasetType.esriDTTable))
            {
                // rename existing table
                var renameTable = outputFeatureWorkspace.OpenTable(GeocodeRoadsTableName);
                // releaser.ManageLifetime(renameTable);

                var outputDataset = (IDataset) renameTable;
                outputDataset.Rename(string.Format("{0}{1}{2}", GeocodeRoadsTableName, "ReplacedOn", DateTime.Now.ToString("yyyyMMdd")));
            }

            // create a feature class in the newly-created file geodatabase
            _geocodeRoads = EsriHelper.CreateFeatureClass(GeocodeRoadsFeatureClassName, null,
                outputFeatureWorkspace);

            // create a table in the newly-created file geodatabase
            var altNames = EsriHelper.CreateTable(GeocodeRoadsTableName, null, outputFeatureWorkspace);

            return output;
        }

        private static void RemoveKeys(IDictionary<string, IndexFieldValue> streetValueMap)
        {
            var unused = new[] {"ALIAS1", "ALIAS2", "ALIAS1TYPE", "ALIAS2TYPE", "ACSNAME", "ACSSUF"};

            foreach (var key in unused)
            {
                streetValueMap.Remove(key);
            }
        }
    }
}