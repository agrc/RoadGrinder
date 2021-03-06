﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using Dapper;
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
        private const string GeocodeRoadsTableName = "AtlNames";
        private const string GeocodeRoadsScratchFeatureClassName = "RoadsScratchData";
        private IWorkspace _scratchFGDB;
        private IFeatureClass _scratchRoadsFC;
        private readonly CliOptions _options;
        private readonly IFeatureClass _roads;
        private IFeatureClass _geocodeRoads;
        private ITable _altnameTableRoads;
        private ITable _altnameTableAddrPnts;

        public AlternateNamesGrinder(IFeatureClass source, CliOptions options)
        {
            _roads = source;
            _options = options;
        }

        public void Grind(IWorkspace output)
        {
            // Check for null values in the SGID Roads and AddressPoints - in the required fields before we begin.
            Console.WriteLine("Check for null values in SGID Roads and AddressPoints: " + DateTime.Now);
            var connectionStringSgid = @"Data Source=" + _options.SgidServer + @";Initial Catalog=" + _options.SgidDatabase + @";User ID=" + _options.SgidId + @";Password=" + _options.SgidId + @"";
            const string checkForNullsRoadsQuery = @"select count(*) from Transportation.Roads (nolock) where CARTOCODE not in ('1','7','99') and NAME is null or POSTTYPE is null or ADDRSYS_L is null or ADDRSYS_R is null or PREDIR is null or POSTDIR is null or A1_NAME is null or A1_POSTTYPE is null or A2_NAME is null or A2_POSTTYPE is null or AN_NAME is null or AN_POSTDIR is null;";
            const string checkForNullsAddressPointsQuery = @"select count(*) from Location.ADDRESSPOINTS (nolock) where AddNum is null or PrefixDir is null or StreetName is null or StreetType is null or SuffixDir is null or AddNumSuffix is null;";
            using (var connection = new SqlConnection(connectionStringSgid))
            {
                connection.Open();
                var rowCount = connection.ExecuteScalar<int>(checkForNullsRoadsQuery);
                if (rowCount != 0)
                {
                    Console.WriteLine(rowCount + " Null Values were found in SGID Roads in one of these fields: ADDRSYS_L, ADDRSYS_R PREDIR, NAME, POSTTYPE, POSTDIR, A1_NAME, A1_POSTTYPE, A2_NAME, A2_POSTTYPE, AN_NAME, AN_POSTDIR.  Remove nulls from SGID.Transportation.Roads and try again.");
                    Console.ReadLine();
                    return;
                }
            }
            using (var connection = new SqlConnection(connectionStringSgid))
            {
                connection.Open();
                var rowCount = connection.ExecuteScalar<int>(checkForNullsAddressPointsQuery);
                if (rowCount != 0)
                {
                    Console.WriteLine(rowCount + " Null Values were found in SGID AddressPoints in one of these fields: AddNum, AddNumSuffix, PrefixDir, StreetName, StreetType, SuffixDir.  Remove nulls from SGID.Location.AddressPoints and try again.");
                    //Console.ReadLine();
                    //return;
                }
            }

            var startTime = DateTime.Now;
            Console.WriteLine("Begin creating Geocode FC: " + DateTime.Now);
            IWorkspaceEdit outputEditWorkspace = null;
            
            try
            {
                var outputFeatureWorkspace = (IFeatureWorkspace)output;

                // create a feature cursor from the source roads data and loop through this subset
                // create the query filter to filter results
                const string geocodableRoads = @"CARTOCODE not in ('1','7','99') and 
                                                    ((FROMADDR_L <> 0 and TOADDR_L <> 0) OR (FROMADDR_R <> 0 and TOADDR_R <> 0)) and 
                                                    NAME <> '' and NAME not like '%ROUNDABOUT%'";

                // GO LIVE...
                //geocodableRoads = @"CARTOCODE not in ('1','7','99') and ((L_F_ADD <> 0 and L_T_ADD <> 0) OR (R_F_ADD <> 0 and R_T_ADD <> 0)) and STREETNAME <> '' and STREETNAME not like '%ROUNDABOUT%'";
                var roadsFilter = new QueryFilter
                {
                    WhereClause = geocodableRoads
                };

                outputEditWorkspace = (IWorkspaceEdit)outputFeatureWorkspace;
                // begin an edit session on the file geodatabase (maybe) that way we can roll back if it errors out
                outputEditWorkspace.StartEditing(false);
                outputEditWorkspace.StartEditOperation();
                
                // create a ComReleaser for feature cursor's life-cycle management                
                using (var comReleaser = new ComReleaser())
                {
                    var roadsCursor = _roads.Search(roadsFilter, false);
                    comReleaser.ManageLifetime(roadsCursor);

                    // begin an edit session on the file geodatabase (maybe) that way we can roll back if it errors out
                    //outputEditWorkspace.StartEditing(false);
                    //outputEditWorkspace.StartEditOperation();

                    IFeature roadFeature;
                    var fieldIndexMap = new FindIndexByNameCommand(_roads, new[]
                    {
                        "ADDRSYS_L", "ADDRSYS_R", "FROMADDR_L", "TOADDR_L", "FROMADDR_R", "TOADDR_R", "PREDIR",
                        "NAME", "POSTTYPE", "POSTDIR", "A1_NAME", "A1_POSTTYPE", "A2_NAME",
                        "A2_POSTTYPE", "AN_NAME", "AN_POSTDIR", "ZIPCODE_L", "ZIPCODE_R", "GlobalID"
                    }).Execute();

                    // loop through the sgid roads' feature cursor
                    while ((roadFeature = roadsCursor.NextFeature()) != null)
                    {
                        var valueMap = new AddValueToIndexFieldMapCommand(fieldIndexMap, roadFeature).Execute();

                        // begin to populate the geocode feature class in the newly-created file geodatabase
                        // check if this segment has a primary streetname
                        if (!string.IsNullOrEmpty(valueMap["NAME"].Value.ToString()))
                        {
                            // only write necessary key/values
                            var streetValueMap = valueMap;
                            //RemoveKeys(streetValueMap);

                            // create a new feature for the primary street name in the roads feature class
                            EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, streetValueMap, true);

                            // create a new feature in the scratch roads fc
                            EsriHelper.InsertFeatureInto(roadFeature, _scratchRoadsFC, streetValueMap, true);
                        }
                        // check if this segment has an alias name
                        if (!string.IsNullOrEmpty(valueMap["A1_NAME"].Value.ToString()))
                        {
                            var aliasValueMap = valueMap;
                            aliasValueMap["NAME"] = aliasValueMap["A1_NAME"];
                            aliasValueMap["POSTTYPE"] = aliasValueMap["A1_POSTTYPE"];

                            // check if primary name is acs, if so remove the sufdir for new alias1-based feature
                            if (!aliasValueMap["NAME"].ToString().Any(char.IsLetter))
                            {
                                // acs primary street
                                aliasValueMap.Remove("POSTDIR");
                            }

                            //RemoveKeys(aliasValueMap);

                            // create a new feature in the altnames table
                            EsriHelper.InsertRowInto(roadFeature,_altnameTableRoads,aliasValueMap, true, false);
                            //EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, aliasValueMap, true);

                            // create a new feature in the scratch roads fc
                            EsriHelper.InsertFeatureInto(roadFeature, _scratchRoadsFC, aliasValueMap, true);
                        }
                        // check if this segment has a second alias name
                        if (!string.IsNullOrEmpty(valueMap["A2_NAME"].Value.ToString()))
                        {
                            var aliasValueMap = valueMap;
                            aliasValueMap["NAME"] = aliasValueMap["A2_NAME"];
                            aliasValueMap["POSTTYPE"] = aliasValueMap["A2_POSTTYPE"];                            
                            
                            // check if primary name is acs, if so remove the sufdir for new alias2-based feature
                            // check if primary name is acs, if so remove the sufdir for new alias1-based feature
                            if (!aliasValueMap["NAME"].ToString().Any(char.IsLetter))
                            {
                                // acs primary street
                                aliasValueMap.Remove("POSTDIR");
                            }

                            //RemoveKeys(aliasValueMap);

                            // create a new feature in the altnames table
                            EsriHelper.InsertRowInto(roadFeature, _altnameTableRoads, aliasValueMap, true, false);
                            //EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, aliasValueMap, true);

                            // create a new feature in the scratch roads table
                            EsriHelper.InsertFeatureInto(roadFeature, _scratchRoadsFC, aliasValueMap, true);
                        }
                        // check if this segment has an acs alias name
                        if (!string.IsNullOrEmpty(valueMap["AN_NAME"].Value.ToString()))
                        {
                            var acsValueMap = valueMap;
                            acsValueMap["NAME"] = acsValueMap["AN_NAME"];
                            acsValueMap["POSTDIR"] = acsValueMap["AN_POSTDIR"];

                            acsValueMap.Remove("POSTTYPE");
                            //RemoveKeys(acsValueMap);

                            // create a new feature in the altnames table
                            EsriHelper.InsertRowInto(roadFeature, _altnameTableRoads, acsValueMap, false, false);
                            //EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, acsValueMap, false);

                            // create a new feature in the scratch roads table
                            EsriHelper.InsertFeatureInto(roadFeature, _scratchRoadsFC, acsValueMap, false);
                        }
                    }
                    outputEditWorkspace.StopEditOperation();
                    outputEditWorkspace.StopEditing(true);
                }

                Console.WriteLine("begin indexing fields: " + DateTime.Now);
                // Add Indexes to query fields.
                string[] fieldsToIndex = { "ADDRSYS_L", "ADDRSYS_R", "FROMADDR_L", "TOADDR_L", "FROMADDR_R", "TOADDR_R",
                    "PREDIR", "NAME", "POSTTYPE", "POSTDIR"};
                foreach (var field in fieldsToIndex)
                {
                    AddIndexToFieldCommand.Execute(_scratchRoadsFC, field + "_IDX", field);                    
                }
                Console.WriteLine("finished indexing fields: " + DateTime.Now);

                // Create the AltNamesRoads table.
                PopulateAltNamesRoadsTableCommand.Execute(outputEditWorkspace, _altnameTableRoads, _scratchRoadsFC);

                // Create the AltNamesAddrPnts table.
                PopulateAltNamesAddrPntsTableCommand.Execute(outputEditWorkspace, _altnameTableAddrPnts, connectionStringSgid);

                Console.WriteLine("Started at: " + startTime);
                Console.WriteLine("Finished at: " + DateTime.Now);
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
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
            var output = EsriHelper.CreateFileGdbWorkspace(_options.OutputGeodatabase, "RoadGrinder");
            // releaser.ManageLifetime(output);

            var outputFeatureWorkspace = (IFeatureWorkspace)output;
            var outputWorkspace2 = (IWorkspace2)output;

            // check if the feature class and table exist in the file geodatabase - if so rename them before adding new data
            if (EsriHelper.NameExists(outputWorkspace2, GeocodeRoadsFeatureClassName, esriDatasetType.esriDTFeatureClass))
            {
                // rename existing fc
                var renameFeatureClass = outputFeatureWorkspace.OpenFeatureClass(GeocodeRoadsFeatureClassName);
                // releaser.ManageLifetime(renameFeatureClass);

                var outputDataset = (IDataset)renameFeatureClass;
                outputDataset.Rename(string.Format("{0}{1}{2:yyyyMMdd}", GeocodeRoadsFeatureClassName, "ReplacedOn", DateTime.Now));
            }

            if (EsriHelper.NameExists(outputWorkspace2, GeocodeRoadsTableName + "Roads", esriDatasetType.esriDTTable))
            {
                // rename existing table
                var renameTable = outputFeatureWorkspace.OpenTable(GeocodeRoadsTableName + "Roads");
                // releaser.ManageLifetime(renameTable);

                var outputDataset = (IDataset)renameTable;
                outputDataset.Rename(string.Format("{0}{1}{2}", GeocodeRoadsTableName + "Roads", "ReplacedOn", DateTime.Now.ToString("yyyyMMdd")));
            }

            if (EsriHelper.NameExists(outputWorkspace2, GeocodeRoadsTableName + "AddrPnts", esriDatasetType.esriDTTable))
            {
                // rename existing table
                var renameTable = outputFeatureWorkspace.OpenTable(GeocodeRoadsTableName + "AddrPnts");
                // releaser.ManageLifetime(renameTable);

                var outputDataset = (IDataset)renameTable;
                outputDataset.Rename(string.Format("{0}{1}{2}", GeocodeRoadsTableName + "AddrPnts", "ReplacedOn", DateTime.Now.ToString("yyyyMMdd")));
            }

            // create a feature class in the newly-created file geodatabase
            _geocodeRoads = EsriHelper.CreateFeatureClass(GeocodeRoadsFeatureClassName, null, outputFeatureWorkspace);

            // create a roads altnames table in the newly-created file geodatabase
            _altnameTableRoads = EsriHelper.CreateTable(GeocodeRoadsTableName + "Roads", null, outputFeatureWorkspace);

            // create a address points altnames table in the newly-created file geodatabase
            _altnameTableAddrPnts = EsriHelper.CreateTable(GeocodeRoadsTableName + "AddrPnts", null, outputFeatureWorkspace);

            // Create the scratch geodatabase for the temp data
            CreateScratchFgdWorkspace();

            return output;
        }

        public void CreateScratchFgdWorkspace()
        {
            // create the file geodatabase for the derived geocoding data
            _scratchFGDB = EsriHelper.CreateFileGdbWorkspace(_options.OutputGeodatabase, "RoadGrinderScratchWS");

            var scratchOutputFeatureWorkspace = (IFeatureWorkspace)_scratchFGDB;
            var outputWorkspace2 = (IWorkspace2)_scratchFGDB;
            var outputFeatureWorkspace = (IFeatureWorkspace)scratchOutputFeatureWorkspace;


            //if (EsriHelper.NameExists(outputWorkspace2, GeocodeRoadsTableName + "RoadsScratchData", esriDatasetType.esriDTTable))
            //{
            //    // rename existing table
            //    var renameTable = outputFeatureWorkspace.OpenTable(GeocodeRoadsTableName + "RoadsScratchData");
            //    // releaser.ManageLifetime(renameTable);

            //    var outputDataset = (IDataset)renameTable;
            //    outputDataset.Rename(string.Format("{0}{1}{2}", GeocodeRoadsTableName + "RoadsScratchData", "ReplacedOn", DateTime.Now.ToString("yyyyMMdd")));
            //}

            _scratchRoadsFC = EsriHelper.CreateFeatureClass(GeocodeRoadsScratchFeatureClassName, null, scratchOutputFeatureWorkspace);

        }

        private static void RemoveKeys(IDictionary<string, IndexFieldValue> streetValueMap)
        {
            var unused = new[] { "A1_NAME", "A2_NAME", "A1_POSTTYPE", "A2_POSTTYPE", "AN_NAME", "AN_POSTDIR" };

            foreach (var key in unused)
            {
                streetValueMap.Remove(key);
            }
        }
    }
}