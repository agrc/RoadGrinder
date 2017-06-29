using System;
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
            const string checkForNullsRoadsQuery = @"select count(*) from Transportation.ROADS where CARTOCODE not in ('1','7','99') and STREETNAME is null or STREETTYPE is null or ADDR_SYS is null or PREDIR is null or SUFDIR is null or ALIAS1 is null or ALIAS1TYPE is null or ALIAS2 is null or ALIAS1TYPE is null or ACSNAME is null or ACSSUF is null;";
            const string checkForNullsAddressPointsQuery = @"select count(*) from Location.ADDRESSPOINTS where AddNum is null or PrefixDir is null or StreetName is null or StreetType is null or SuffixDir is null or AddNumSuffix is null;";
            using (var connection = new SqlConnection(connectionStringSgid))
            {
                connection.Open();
                var rowCount = connection.ExecuteScalar<int>(checkForNullsRoadsQuery);
                if (rowCount != 0)
                {
                    Console.WriteLine(rowCount + " Null Values were found in SGID Roads in one of these fields: ADDR_SYS, PREDIR, STREETNAME, STREETTYPE, SUFDIR, ALIAS1, ALIAS1TYPE, ALIAS2, ALIAS1TYPE, ACSNAME, ACSSUF.  Remove nulls from SGID.Transportation.Roads and try again.");
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
                    Console.ReadLine();
                    return;
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

                            // check if primary name is acs, if so remove the sufdir for new alias1-based feature
                            if (!aliasValueMap["STREETNAME"].ToString().Any(char.IsLetter))
                            {
                                // acs primary street
                                aliasValueMap.Remove("SUFDIR");
                            }

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
                            
                            // check if primary name is acs, if so remove the sufdir for new alias2-based feature
                            // check if primary name is acs, if so remove the sufdir for new alias1-based feature
                            if (!aliasValueMap["STREETNAME"].ToString().Any(char.IsLetter))
                            {
                                // acs primary street
                                aliasValueMap.Remove("SUFDIR");
                            }

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

                Console.WriteLine("begin indexing fields: " + DateTime.Now);
                // Add Indexes to query fields.
                string[] fieldsToIndex = { "ADDRSYS_L", "ADDRSYS_R", "FROMADDR_L", "TOADDR_L", "FROMADDR_R", "TOADDR_R",
                    "PREDIR", "NAME", "POSTTYPE", "POSTDIR"};
                foreach (var field in fieldsToIndex)
                {
                    AddIndexToFieldCommand.Execute(_geocodeRoads, field + "_IDX", field);                    
                }
                Console.WriteLine("finished indexing fields: " + DateTime.Now);

                // Create the AltNamesRoads table.
                PopulateAltNamesRoadsTableCommand.Execute(outputEditWorkspace, _altnameTableRoads, _geocodeRoads);

                #region make this a seperate command (LoadAltNamesRoads), passing in the table name and workspace
//                // load records into the altnames table for roads
//                Console.WriteLine("begin altnames table for roads: " + DateTime.Now);
//                // get feature cursor of newly-created derived-roads fgdb feature class
//                using (var comReleaser = new ComReleaser())
//                {
//                    int consoleCounter = 0;
//                    outputEditWorkspace.StartEditing(false);
//                    outputEditWorkspace.StartEditOperation();
         
//                    // Set up where clause to omit the records without a predir - as they are already in the database without a predir
//                    //var omitPredirQueryFilter = new QueryFilter {WhereClause = @"PREDIR <> ''"};
//                    // this one only gets the alpha named roads to evaluate for the altnames table
//                    var omitPredirQueryFilter = new QueryFilter { WhereClause = @"PREDIR <> '' and (UPPER(""NAME"") <> ""NAME"" OR LOWER(""NAME"") <> ""NAME"")" };

//                    var geocodeRoadsCursor = _geocodeRoads.Search(omitPredirQueryFilter, false);
//                    comReleaser.ManageLifetime(geocodeRoadsCursor);

//                    IFeature geocodeRoadFeature;

//                    // loop through the geocode roads feature cursor
//                    while ((geocodeRoadFeature = geocodeRoadsCursor.NextFeature()) != null)
//                    {
//                        // check if this segment is found in another address quad, within the same address grid
//                        using (var comReleaser2 = new ComReleaser())
//                        {
//                            var fieldIndexMapNewSchema = new FindIndexByNameCommand(_geocodeRoads, new[]
//                            {
//                                "ADDRSYS_L", "ADDRSYS_R", "FROMADDR_L", "TOADDR_L", "FROMADDR_R", "TOADDR_R",
//                                "PREDIR", "NAME", "POSTTYPE", "POSTDIR", "ZIPCODE_L", "ZIPCODE_R", "GLOBALID_SGID"
//                            }).Execute();

//                            var valueMapNewSchema = new AddValueToIndexFieldMapCommand(fieldIndexMapNewSchema, geocodeRoadFeature).Execute();

//                            // set up query filter 
//                            var possibleCandidatesFilter = new QueryFilter
//                            {
//                                WhereClause = @"(ADDRSYS_L = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("ADDRSYS_L"))).ToString() + @"' AND 
//                                                ADDRSYS_R = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("ADDRSYS_R"))).ToString() + @"' AND 
//                                                NAME = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("NAME"))).ToString() + @"' AND 
//                                                POSTTYPE = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("POSTTYPE"))).ToString() + @"' AND 
//                                                POSTDIR = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("POSTDIR"))).ToString() + @"' AND 
//                                                PREDIR <> '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("PREDIR"))).ToString() + @"')"
//                            };

//                            var possibleCandidatesFeatureCursor = _geocodeRoads.Search(possibleCandidatesFilter, false);
//                            comReleaser2.ManageLifetime(possibleCandidatesFeatureCursor);

//                            // Check if matching seg was found.
//                            var possibleCandidateFeature = possibleCandidatesFeatureCursor.NextFeature();
//                            bool foundMatch = false;

//                            // Check if a segment was found in another quad with the same characteristics.
//                            if (possibleCandidateFeature != null)
//                            {

//                                ////Console.WriteLine(consoleCounter + ": found in the other grid: " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("OBJECTID")).ToString());
//                                // A feature was found.
//                                while (possibleCandidateFeature != null)
//                                {
//                                    using (var comReleaser3 = new ComReleaser())
//                                    {
//                                        // Check if any of the four numbers from the geocodeRoadsFeature class is contained in any of these returned possible candidates
//                                        // Get the lowest and the highest number
//                                        int possibleCandidateOID = Convert.ToInt32(possibleCandidateFeature.get_Value(possibleCandidateFeature.Fields.FindField("OBJECTID")));
//                                        int fromAddrL = Convert.ToInt32(geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("FROMADDR_L")));
//                                        int fromAddrR = Convert.ToInt32(geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("FROMADDR_R")));
//                                        int toAddrL = Convert.ToInt32(geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("TOADDR_L")));
//                                        int toAddrR = Convert.ToInt32(geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("TOADDR_R")));                                    
//                                        int highNum; 
//                                        int lowNum;

//                                        // Asign the high and low numbers (make sure the low is not zero)
//                                        if (fromAddrL == 0 || fromAddrR == 0)
//                                        {
//                                            if (fromAddrL == 0)
//                                            {
//                                                lowNum = fromAddrR;
//                                            }
//                                            else
//                                            {
//                                                lowNum = fromAddrL;
//                                            }
//                                        }
//                                        else
//                                        {
//                                            if (fromAddrL < fromAddrR)
//                                            {
//                                                lowNum = fromAddrL;
//                                            }
//                                            else
//                                            {
//                                                lowNum = fromAddrR;
//                                            }
//                                        }

//                                        if (toAddrL > toAddrR)
//                                        {
//                                            highNum = toAddrL;
//                                        }
//                                        else
//                                        {
//                                            highNum = toAddrR;
//                                        }

//                                        // Create a new query filter to see if the high or low number falls within one of these candidate features
//                                        var fitsInLargerSegmentFilter = new QueryFilter
//                                        {
//                                            WhereClause = @"OBJECTID = " + possibleCandidateOID + @" AND 
//                                                        (((" + lowNum + @" BETWEEN FROMADDR_L  AND TOADDR_L ) or (" + highNum + @" BETWEEN FROMADDR_L  AND TOADDR_L)) or 
//                                                        ((" + lowNum + @" BETWEEN FROMADDR_R  AND TOADDR_R ) or (" + highNum + @"  BETWEEN FROMADDR_R  AND TOADDR_R)))"
//                                        };

//                                        //Console.WriteLine("for oid: " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("OBJECTID")).ToString() + " " +  fitsInLargerSegmentFilter.WhereClause.ToString());

//                                        var fitsInLargerSegmentFeatureCursor = _geocodeRoads.Search(fitsInLargerSegmentFilter, false);
//                                        comReleaser3.ManageLifetime(fitsInLargerSegmentFeatureCursor);

//                                        // Check if matching seg was found.
//                                        var fitsInLargerFeature = fitsInLargerSegmentFeatureCursor.NextFeature();

//                                        if (fitsInLargerFeature != null)
//                                        {
//                                            // A match was found.  So don't write it to the AltNames table
//                                            consoleCounter = consoleCounter + 1;
//                                            Console.WriteLine(consoleCounter + ": fit in larger: " + fitsInLargerFeature.get_Value(fitsInLargerFeature.Fields.FindField("OBJECTID")).ToString());
//                                            foundMatch = true;
//                                            break;
//                                        }
//                                        else
//                                        {
//                                            // the current segment's address range did not fit within a larger segment
//                                            // check if a smaller address ranged segment fits into this larger segment
//                                            using (var comReleaser4 = new ComReleaser())
//                                            {
//                                                // Get the lowest and the highest number of the possible candidate
//                                                int geocodeRoadOID_ = Convert.ToInt32(geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("OBJECTID")));
//                                                int fromAddrL_ = Convert.ToInt32(possibleCandidateFeature.get_Value(possibleCandidateFeature.Fields.FindField("FROMADDR_L")));
//                                                int fromAddrR_ = Convert.ToInt32(possibleCandidateFeature.get_Value(possibleCandidateFeature.Fields.FindField("FROMADDR_R")));
//                                                int toAddrL_ = Convert.ToInt32(possibleCandidateFeature.get_Value(possibleCandidateFeature.Fields.FindField("TOADDR_L")));
//                                                int toAddrR_ = Convert.ToInt32(possibleCandidateFeature.get_Value(possibleCandidateFeature.Fields.FindField("TOADDR_R")));
//                                                int highNum_;
//                                                int lowNum_;

//                                                // Asign the high and low numbers (make sure the low is not zero)
//                                                if (fromAddrL_ == 0 || fromAddrR_ == 0)
//                                                {
//                                                    if (fromAddrL_ == 0)
//                                                    {
//                                                        lowNum_ = fromAddrR_;
//                                                    }
//                                                    else
//                                                    {
//                                                        lowNum_ = fromAddrL_;
//                                                    }
//                                                }
//                                                else
//                                                {
//                                                    if (fromAddrL_ < fromAddrR_)
//                                                    {
//                                                        lowNum_ = fromAddrL_;
//                                                    }
//                                                    else
//                                                    {
//                                                        lowNum_ = fromAddrR_;
//                                                    }
//                                                }
                                                
//                                                if (toAddrL_ > toAddrR_)
//                                                {
//                                                    highNum_ = toAddrL_;
//                                                }
//                                                else
//                                                {
//                                                    highNum_ = toAddrR_;
//                                                }

                                                
//                                                var smallerCandidateFitsInFilter = new QueryFilter
//                                                {
//                                                    WhereClause = @"OBJECTID = " + geocodeRoadOID_ + @" AND 
//                                                        (((" + lowNum_ + @" BETWEEN FROMADDR_L  AND TOADDR_L ) or (" + highNum_ + @" BETWEEN FROMADDR_L  AND TOADDR_L)) or 
//                                                        ((" + lowNum_ + @" BETWEEN FROMADDR_R  AND TOADDR_R ) or (" + highNum_ + @"  BETWEEN FROMADDR_R  AND TOADDR_R)))"
//                                                };

//                                                var smallerCandidateFitsInFeatureCursor = _geocodeRoads.Search(smallerCandidateFitsInFilter, false);
//                                                comReleaser4.ManageLifetime(smallerCandidateFitsInFeatureCursor);

//                                                // Check if matching seg was found.
//                                                var smallerCandidateFitsInFeature = smallerCandidateFitsInFeatureCursor.NextFeature();

//                                                if (smallerCandidateFitsInFeature != null)
//                                                {
//                                                    // A match was found.  So don't write it to the AltNames table
//                                                    consoleCounter = consoleCounter + 1;
//                                                    Console.WriteLine(consoleCounter + ": candidate fit within it: " + smallerCandidateFitsInFeature.get_Value(smallerCandidateFitsInFeature.Fields.FindField("OBJECTID")).ToString());
//                                                    foundMatch = true;
//                                                    break;          
//                                                }
//                                            }
//                                        }
//                                    }

//                                    // Advance to the next feature in the cursor.
//                                    possibleCandidateFeature = possibleCandidatesFeatureCursor.NextFeature();
//                                }
//                            }
//                            else
//                            {
//                                // No possible candidates were found in other quad, so add this segment to the AltNames table
//                            }

//                            // Write to the AltNames table > match was not found
//                            if (foundMatch == false)
//                            {
//                                // A matching feature was not found.
//                                // Add a record to the table without a predir.
//                                // Remove the PREDIR value from the field map
//                                var valueMapNewSchemaNoPredir = valueMapNewSchema;
//                                valueMapNewSchemaNoPredir.Remove("PREDIR");
//                                EsriHelper.InsertRowInto(geocodeRoadFeature, _altnameTableRoads, valueMapNewSchema);

//                                consoleCounter = consoleCounter + 1;
//                                Console.WriteLine(consoleCounter + ": not found in the other grid: " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("OBJECTID")).ToString());
//                            }
//                        }
//                    }
//                    // stop editing from altnames road table (this allow the roads altnames table to be preserved even if the altnames for addr pnts (below) errors out)
//                    outputEditWorkspace.StopEditOperation();
//                    outputEditWorkspace.StopEditing(true);
//                }
                #endregion

                // Create the AltNamesAddrPnts table.
                PopulateAltNamesAddrPntsTableCommand.Execute(outputEditWorkspace, _altnameTableAddrPnts, connectionStringSgid);

                #region make this a seperate command (LoadAltNamesAddrPnts), passing in the table name and workspace
                ////// load records into the altnames table for address points
                //////start editing again
                ////outputEditWorkspace.StartEditing(false);
                ////outputEditWorkspace.StartEditOperation();

                ////int counter = 0;

                ////Console.WriteLine("begin altnames table for addr pnts: " + DateTime.Now);
                //////var connectionStringSgid = @"Data Source=" + _options.SgidServer + @";Initial Catalog=" + _options.SgidDatabase + @";User ID=" + _options.SgidId + @";Password=" + _options.SgidId + @"";

                //////const string sgidAddrPntstoVerifyQuery = @"SELECT DISTINCT AddSystem,UTAddPtID,AddNum,AddNumSuffix,PrefixDir,StreetName,StreetType,SuffixDir,UnitType, UnitID, City, ZipCode, CountyID FROM LOCATION.ADDRESSPOINTS WHERE PrefixDir <> '' AND StreetName LIKE '%[A-Z]%' AND StreetName NOT LIKE 'HIGHWAY %' and AddSystem = 'SALT LAKE CITY' AND StreetName = 'STATE';";
                ////const string sgidAddrPntstoVerifyQuery = @"SELECT DISTINCT AddSystem,AddNum,AddNumSuffix,PrefixDir,StreetName,StreetType,SuffixDir,City,ZipCode,CountyID FROM LOCATION.ADDRESSPOINTS WHERE PrefixDir <> '' AND StreetName LIKE '%[A-Z]%' AND StreetName NOT LIKE 'HIGHWAY %' AND AddSystem = 'SALT LAKE CITY';";
                ////using (var con = new SqlConnection(connectionStringSgid))
                ////{
                ////    con.Open();
                ////    var sgidAddrPntsToVerifyList = con.Query(sgidAddrPntstoVerifyQuery);

                ////    // loop through potential altname address points (from sgid records) 
                ////    var sgidAddrPntsList = sgidAddrPntsToVerifyList as dynamic[] ?? sgidAddrPntsToVerifyList.ToArray();
                ////    foreach (var sgidAddrPntToVerify in sgidAddrPntsList)
                ////    {
                ////        // check for matching address point in same address-system with different prefix
                ////        // Check is the SuffixDir is empty or null before creating the query
                ////        //string matchingAddrPntQuery = @"SELECT OBJECTID FROM LOCATION.ADDRESSPOINTS WHERE AddSystem = '" + sgidAddrPntToVerify.AddSystem + @"' AND AddNum = '" + sgidAddrPntToVerify.AddNum + @"' AND PrefixDir <> '" + sgidAddrPntToVerify.PrefixDir + @"' AND StreetName = '" + sgidAddrPntToVerify.StreetName + @"' AND StreetType = '" + sgidAddrPntToVerify.StreetType + @"' AND IsNull(SuffixDir, '') = '';";
                ////        string matchingAddrPntQuery = @"SELECT Count(*) Count FROM LOCATION.ADDRESSPOINTS WHERE AddSystem = '" + sgidAddrPntToVerify.AddSystem + @"' AND AddNum = '" + sgidAddrPntToVerify.AddNum + @"' AND PrefixDir <> '" + sgidAddrPntToVerify.PrefixDir + @"' AND StreetName = '" + sgidAddrPntToVerify.StreetName + @"' AND StreetType = '" + sgidAddrPntToVerify.StreetType + @"' AND SuffixDir = '" + sgidAddrPntToVerify.SuffixDir + @"' AND AddNumSuffix = '" + sgidAddrPntToVerify.AddNumSuffix + @"';"; 
                ////        //string matchingAddrPntQuery = @"SELECT * FROM LOCATION.ADDRESSPOINTS WHERE AddSystem = '" + sgidAddrPntToVerify.AddSystem + @"' AND AddNum = '" + sgidAddrPntToVerify.AddNum + @"' AND PrefixDir <> '" + sgidAddrPntToVerify.PrefixDir + @"' AND StreetName = '" + sgidAddrPntToVerify.StreetName + @"' AND StreetType = '" + sgidAddrPntToVerify.StreetType + @"' AND SuffixDir = '" + sgidAddrPntToVerify.SuffixDir + @"'";
                ////        //if (sgidAddrPntToVerify.SuffixDir == null)
                ////        //{
                ////        //    matchingAddrPntQuery = @"SELECT OBJECTID FROM LOCATION.ADDRESSPOINTS WHERE AddSystem = '" + sgidAddrPntToVerify.AddSystem + @"' AND AddNum = '" + sgidAddrPntToVerify.AddNum + @"' AND PrefixDir <> '" + sgidAddrPntToVerify.PrefixDir + @"' AND StreetName = '" + sgidAddrPntToVerify.StreetName + @"' AND StreetType = '" + sgidAddrPntToVerify.StreetType + @"' AND SuffixDir is (string)null";                            
                ////        //}
                ////        //else
                ////        //{
                ////        //    matchingAddrPntQuery = @"SELECT OBJECTID FROM LOCATION.ADDRESSPOINTS WHERE AddSystem = '" + sgidAddrPntToVerify.AddSystem + @"' AND AddNum = '" + sgidAddrPntToVerify.AddNum + @"' AND PrefixDir <> '" + sgidAddrPntToVerify.PrefixDir + @"' AND StreetName = '" + sgidAddrPntToVerify.StreetName + @"' AND StreetType = '" + sgidAddrPntToVerify.StreetType + @"' AND SuffixDir = '" + sgidAddrPntToVerify.SuffixDir + @"'";         
                ////        //}
                        
                ////        using (var con1 = new SqlConnection(connectionStringSgid))
                ////        {
                ////            con1.Open();
                ////            var rowCount = con1.ExecuteScalar<int>(matchingAddrPntQuery);

                ////            if (rowCount == 0)
                ////            {
                ////                counter = counter + 1;
                ////                Console.WriteLine(counter + ": AddressPoints: not found with other predir.");
                ////                IDictionary<string, object> dictRow =
                ////                    sgidAddrPntToVerify as IDictionary<string, object>;
                ////                //dictRow.Remove("OBJECTID");
                ////                dictRow.Remove("PrefixDir");
                ////                EsriHelper.InsertRowInto(_altnameTableAddrPnts, dictRow);                                
                ////            }
                ////            else
                ////            {
                ////                Console.WriteLine(counter + ": AddressPoints: found with other predir - don't add to altnames table.");
                ////            }

                ////            //var matchingAddrPntList = con1.Query(matchingAddrPntQuery);
                ////            //var matchCount = matchingAddrPntList as dynamic[] ?? matchingAddrPntList.ToArray();
                ////            //foreach (var match in matchCount)
                ////            //{
                ////            //    if (match.Count == 0)
                ////            //    {

                ////            //    }
                ////            //    else
                ////            //    {
                ////            //        Console.WriteLine(counter + ": AddressPoints: found with other predir - don't add to altnames table.");
                ////            //    }                                
                ////            //}



                ////            ////var dict = con1.Query(matchingAddrPntQuery).ToDictionary(
                ////            ////    row => (string)row.UniqueString,
                ////            ////    row => (string)row.Id);

                ////            //if (dict.Count == 0)
                ////            //{
                ////            //    counter = counter + 1;
                ////            //    Console.WriteLine(counter + ": AddressPoints: not found with other predir.");
                ////            //    IDictionary<string, object> dictRow =
                ////            //        sgidAddrPntToVerify as IDictionary<string, object>;
                ////            //    //dictRow.Remove("OBJECTID");
                ////            //    dictRow.Remove("PrefixDir");
                ////            //    EsriHelper.InsertRowInto(_altnameTableAddrPnts, dictRow);
                ////            //}
                ////            //else
                ////            //{
                ////            //    counter = counter + 1;
                ////            //    Console.WriteLine(counter + ": AddressPoints: found with other predir - don't add to altnames table.");
                ////            //}
                ////        }
                ////    }
                ////    // stop editing from altnames addr pnts table
                ////    outputEditWorkspace.StopEditOperation();
                ////    outputEditWorkspace.StopEditing(true);
                ////}
                #endregion

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