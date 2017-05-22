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
        private const string GeocodeRoadsTableName = "AtlNames";
        private readonly CliOptions _options;
        private readonly IFeatureClass _roads;
        private IFeatureClass _geocodeRoads;
        private ITable _altnameTable;

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
                var outputFeatureWorkspace = (IFeatureWorkspace)output;

                // create a feature cursor from the source roads data and loop through this subset
                // create the query filter to filter results
                // FOR TESTING...                 
                const string geocodableRoads = @"ADDR_SYS = 'PROVO' and CARTOCODE not in ('1','7','99') and 
                                                    ((L_F_ADD <> 0 and L_T_ADD <> 0) OR (R_F_ADD <> 0 and R_T_ADD <> 0)) and 
                                                    STREETNAME <> '' and STREETNAME not like '%ROUNDABOUT%'";

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
                            //RemoveKeys(streetValueMap);

                            // create a new feature
                            EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, streetValueMap, true);
                        }
                        // check if this segment has an alias name
                        if (!string.IsNullOrEmpty(valueMap["ALIAS1"].Value.ToString()))
                        {
                            var aliasValueMap = valueMap;
                            aliasValueMap["STREETNAME"] = aliasValueMap["ALIAS1"];
                            aliasValueMap["STREETTYPE"] = aliasValueMap["ALIAS1TYPE"];

                            //RemoveKeys(aliasValueMap);

                            // create a new feature
                            EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, aliasValueMap, true);
                        }
                        // check if this segment has a second alias name
                        if (!string.IsNullOrEmpty(valueMap["ALIAS2"].Value.ToString()))
                        {
                            var aliasValueMap = valueMap;
                            aliasValueMap["STREETNAME"] = aliasValueMap["ALIAS2"];
                            aliasValueMap["STREETTYPE"] = aliasValueMap["ALIAS2TYPE"];

                            //RemoveKeys(aliasValueMap);

                            // create a new feature
                            EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, aliasValueMap, true);
                        }
                        // check if this segment has an acs alias name
                        if (!string.IsNullOrEmpty(valueMap["ACSNAME"].Value.ToString()))
                        {
                            var acsValueMap = valueMap;
                            acsValueMap["STREETNAME"] = acsValueMap["ACSNAME"];
                            acsValueMap["SUFDIR"] = acsValueMap["ACSSUF"];

                            acsValueMap.Remove("STREETTYPE");
                            //RemoveKeys(acsValueMap);

                            // create a new feature
                            EsriHelper.InsertFeatureInto(roadFeature, _geocodeRoads, acsValueMap, false);
                        }
                    }
                    outputEditWorkspace.StopEditOperation();
                    outputEditWorkspace.StopEditing(true);
                }

                // create the altnames table
                Console.WriteLine("begin altnames table");
                // get feature cursor of newly-created derived-roads fgdb feature class
                using (var comReleaser = new ComReleaser())
                {
                    int consoleCounter = 0;
                    outputEditWorkspace.StartEditing(false);
                    outputEditWorkspace.StartEditOperation();
         
                    // Set up where clause to omit the records without a predir - as they are already in the database without a predir
                    var omitPredirQueryFilter = new QueryFilter {WhereClause = @"PREDIR <> ''"};

                    var geocodeRoadsCursor = _geocodeRoads.Search(omitPredirQueryFilter, false);
                    comReleaser.ManageLifetime(geocodeRoadsCursor);

                    IFeature geocodeRoadFeature;

                    // loop through the geocode roads feature cursor
                    while ((geocodeRoadFeature = geocodeRoadsCursor.NextFeature()) != null)
                    {
                        // check if this segment is found in another address quad, within the same address grid
                        using (var comReleaser2 = new ComReleaser())
                        {
                            var fieldIndexMapNewSchema = new FindIndexByNameCommand(_geocodeRoads, new[]
                            {
                                "ADDRSYS_L", "ADDRSYS_R", "FROMADDR_L", "TOADDR_L", "FROMADDR_R", "TOADDR_R",
                                "PREDIR", "NAME", "POSTTYPE", "POSTDIR", "ZIPCODE_L", "ZIPCODE_R", "GLOBALID_SGID"
                            }).Execute();

                            var valueMapNewSchema = new AddValueToIndexFieldMapCommand(fieldIndexMapNewSchema, geocodeRoadFeature).Execute();

                            // set up query filter 
                            var possibleCandidatesFilter = new QueryFilter
                            {
//                                WhereClause = @"(ADDRSYS_L = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("ADDRSYS_L"))).ToString() + @"' AND 
//                                                ADDRSYS_R = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("ADDRSYS_R"))).ToString() + @"' AND 
//                                                NAME = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("NAME"))).ToString() + @"' AND 
//                                                POSTTYPE = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("POSTTYPE"))).ToString() + @"' AND 
//                                                POSTDIR = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("POSTDIR"))).ToString() + @"' AND 
//                                                PREDIR <> '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("PREDIR"))).ToString() + @"') AND 
//                                                (((FROMADDR_L BETWEEN " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("FROMADDR_L"))) + @" AND " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("TOADDR_L"))) + @") OR 
//                                                (TOADDR_L BETWEEN " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("FROMADDR_L"))) + @" AND " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("TOADDR_L"))) + @")) OR
//                                                ((FROMADDR_R BETWEEN " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("FROMADDR_R"))) + @" AND " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("TOADDR_R"))) + @") OR 
//                                                (TOADDR_R BETWEEN " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("FROMADDR_R"))) + @" AND " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("TOADDR_R"))) + @")))"
//                            };

                                WhereClause = @"(ADDRSYS_L = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("ADDRSYS_L"))).ToString() + @"' AND 
                                                ADDRSYS_R = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("ADDRSYS_R"))).ToString() + @"' AND 
                                                NAME = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("NAME"))).ToString() + @"' AND 
                                                POSTTYPE = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("POSTTYPE"))).ToString() + @"' AND 
                                                POSTDIR = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("POSTDIR"))).ToString() + @"' AND 
                                                PREDIR <> '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("PREDIR"))).ToString() + @"')"
                            };

                            var possibleCandidatesFeatureCursor = _geocodeRoads.Search(possibleCandidatesFilter, false);
                            comReleaser2.ManageLifetime(possibleCandidatesFeatureCursor);

                            // Check if matching seg was found.
                            var possibleCandidateFeature = possibleCandidatesFeatureCursor.NextFeature();

                            // Check if a segment was found in another quad with the same characteristics.
                            if (possibleCandidateFeature != null)
                            {
                                bool matchFound = false;
                                ////Console.WriteLine(consoleCounter + ": found in the other grid: " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("OBJECTID")).ToString());
                                // A feature was found.
                                while (possibleCandidateFeature != null)
                                {
                                    using (var comReleaser3 = new ComReleaser())
                                    {
                                        // Check if any of the four numbers from the geocodeRoadsFeature class is contained in any of these returned possible candidates
                                        // Get the lowest and the highest number
                                        int possibleCandidateOID = Convert.ToInt32(possibleCandidateFeature.get_Value(possibleCandidateFeature.Fields.FindField("OBJECTID")));
                                        int fromAddrL = Convert.ToInt32(geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("FROMADDR_L")));
                                        int fromAddrR = Convert.ToInt32(geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("FROMADDR_R")));
                                        int toAddrL = Convert.ToInt32(geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("TOADDR_L")));
                                        int toAddrR = Convert.ToInt32(geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("TOADDR_R")));                                    
                                        int highNum; 
                                        int lowNum;
                                    
                                        // Asign the high and low numbers
                                        if (fromAddrL < fromAddrR)
                                        {
                                            lowNum = fromAddrL;
                                        }
                                        else
                                        {
                                            lowNum = fromAddrR;
                                        }

                                        if (toAddrL > toAddrR)
                                        {
                                            highNum = toAddrL;
                                        }
                                        else
                                        {
                                            highNum = toAddrR;
                                        }

                                        // Create a new query filter to see if the high or low number falls within one of these candidate features
                                        var findMatchFilter = new QueryFilter
                                        {
                                            WhereClause = @"OBJECTID = " + possibleCandidateOID + @" AND 
                                                        (((" + lowNum + @" BETWEEN FROMADDR_L  AND TOADDR_L ) or (" + highNum + @" BETWEEN FROMADDR_L  AND TOADDR_L)) or 
                                                        ((" + lowNum + @" BETWEEN FROMADDR_R  AND TOADDR_R ) or (" + highNum + @"  BETWEEN FROMADDR_R  AND TOADDR_R)))"
                                        };

                                        Console.WriteLine("for oid: " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("OBJECTID")).ToString() + " " +  findMatchFilter.WhereClause.ToString());

                                        var findMatchFeatureCursor = _geocodeRoads.Search(findMatchFilter, false);
                                        comReleaser3.ManageLifetime(findMatchFeatureCursor);

                                        // Check if matching seg was found.
                                        var findMatchFeature = findMatchFeatureCursor.NextFeature();

                                        if (findMatchFeature != null)
                                        {
                                            // A match was found.  So don't write it to the AltNames table
                                            Console.WriteLine("match Found: " + findMatchFeature.get_Value(findMatchFeature.Fields.FindField("OBJECTID")).ToString());
                                            matchFound = true;
                                            break;
                                        }
                                    }

                                    // Advance to the next feature in the cursor.
                                    possibleCandidateFeature = possibleCandidatesFeatureCursor.NextFeature();
                                }

                                // Write to the AltNames table > match was not found
                                if (matchFound == false)
                                {
                                    // A matching feature was not found.
                                    // Add a record to the table without a predir.
                                    // Remove the PREDIR value from the field map
                                    var valueMapNewSchemaNoPredir = valueMapNewSchema;
                                    valueMapNewSchemaNoPredir.Remove("PREDIR");
                                    EsriHelper.InsertRowInto(geocodeRoadFeature, _altnameTable, valueMapNewSchema);

                                    consoleCounter = consoleCounter + 1;
                                    Console.WriteLine(consoleCounter + ": not found in the other grid: " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("OBJECTID")).ToString());
                                }
                            }
                            else
                            {
                                
                                //////// figure out what to do if the source road does not have a predir but then finds a matching seg with a predir in another quad 
                                // save edits after the geocode table is done, before we start the altnames table
                                
                                ////consoleCounter = consoleCounter + 1;
                                ////Console.WriteLine(consoleCounter + ": not found in the other grid: " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("OBJECTID")).ToString());
                                ////// A matching feature was not found.
                                ////// Add a record to the table without a predir.
                                ////// Remove the PREDIR value from the field map
                                ////var valueMapNewSchemaNoPredir = valueMapNewSchema;
                                ////valueMapNewSchemaNoPredir.Remove("PREDIR");
                                ////EsriHelper.InsertRowInto(geocodeRoadFeature, _altnameTable, valueMapNewSchema);
                            }


                            //while ((roadCrossesQuadFeature = roadCrossesQuadCursor.NextFeature()) != null)
                            //{


                            //}
                        }

                    }
                    // stop editing from altnames table
                    outputEditWorkspace.StopEditOperation();
                    outputEditWorkspace.StopEditing(true);
                }

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
            var output = EsriHelper.CreateFileGdbWorkspace(_options.OutputGeodatabase, GeocodeRoadsFeatureClassName);
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

            if (EsriHelper.NameExists(outputWorkspace2, GeocodeRoadsTableName, esriDatasetType.esriDTTable))
            {
                // rename existing table
                var renameTable = outputFeatureWorkspace.OpenTable(GeocodeRoadsTableName);
                // releaser.ManageLifetime(renameTable);

                var outputDataset = (IDataset)renameTable;
                outputDataset.Rename(string.Format("{0}{1}{2}", GeocodeRoadsTableName, "ReplacedOn", DateTime.Now.ToString("yyyyMMdd")));
            }

            // create a feature class in the newly-created file geodatabase
            _geocodeRoads = EsriHelper.CreateFeatureClass(GeocodeRoadsFeatureClassName, null, outputFeatureWorkspace);

            // create a table in the newly-created file geodatabase
            _altnameTable = EsriHelper.CreateTable(GeocodeRoadsTableName, null, outputFeatureWorkspace);

            return output;
        }

        private static void RemoveKeys(IDictionary<string, IndexFieldValue> streetValueMap)
        {
            var unused = new[] { "ALIAS1", "ALIAS2", "ALIAS1TYPE", "ALIAS2TYPE", "ACSNAME", "ACSSUF" };

            foreach (var key in unused)
            {
                streetValueMap.Remove(key);
            }
        }

    }
}