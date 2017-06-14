using ESRI.ArcGIS.Geodatabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ESRI.ArcGIS.ADF;
using RoadGrinder.services;

namespace RoadGrinder.commands
{
    public static class PopulateAltNamesRoadsTableCommand
    {
        public static void Execute(IWorkspaceEdit outputEditWorkspace, ITable altnameTableRoads, IFeatureClass geocodeRoads )
        {
            // load records into the altnames table for roads
            Console.WriteLine("begin altnames table for roads: " + DateTime.Now);
            // get feature cursor of newly-created derived-roads fgdb feature class
            using (var comReleaser = new ComReleaser())
            {
                int consoleCounter = 0;
                outputEditWorkspace.StartEditing(false);
                outputEditWorkspace.StartEditOperation();

                // Set up where clause to omit the records without a predir - as they are already in the database without a predir
                //var omitPredirQueryFilter = new QueryFilter {WhereClause = @"PREDIR <> ''"};
                // this one only gets the alpha named roads to evaluate for the altnames table
                var omitPredirQueryFilter = new QueryFilter { WhereClause = @"PREDIR <> '' and (UPPER(""NAME"") <> ""NAME"" OR LOWER(""NAME"") <> ""NAME"")" };

                var geocodeRoadsCursor = geocodeRoads.Search(omitPredirQueryFilter, false);
                comReleaser.ManageLifetime(geocodeRoadsCursor);

                IFeature geocodeRoadFeature;

                // loop through the geocode roads feature cursor
                while ((geocodeRoadFeature = geocodeRoadsCursor.NextFeature()) != null)
                {
                    // check if this segment is found in another address quad, within the same address grid
                    using (var comReleaser2 = new ComReleaser())
                    {
                        var fieldIndexMapNewSchema = new FindIndexByNameCommand(geocodeRoads, new[]
                        {
                            "ADDRSYS_L", "ADDRSYS_R", "FROMADDR_L", "TOADDR_L", "FROMADDR_R", "TOADDR_R",
                            "PREDIR", "NAME", "POSTTYPE", "POSTDIR", "ZIPCODE_L", "ZIPCODE_R", "GLOBALID_SGID"
                        }).Execute();

                        var valueMapNewSchema = new AddValueToIndexFieldMapCommand(fieldIndexMapNewSchema, geocodeRoadFeature).Execute();

                        // set up query filter 
                        var possibleCandidatesFilter = new QueryFilter
                        {
                            WhereClause = @"(ADDRSYS_L = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("ADDRSYS_L"))).ToString() + @"' AND 
                                                ADDRSYS_R = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("ADDRSYS_R"))).ToString() + @"' AND 
                                                NAME = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("NAME"))).ToString() + @"' AND 
                                                POSTTYPE = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("POSTTYPE"))).ToString() + @"' AND 
                                                POSTDIR = '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("POSTDIR"))).ToString() + @"' AND 
                                                PREDIR <> '" + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField(("PREDIR"))).ToString() + @"')"
                        };

                        var possibleCandidatesFeatureCursor = geocodeRoads.Search(possibleCandidatesFilter, false);
                        comReleaser2.ManageLifetime(possibleCandidatesFeatureCursor);

                        // Check if matching seg was found.
                        var possibleCandidateFeature = possibleCandidatesFeatureCursor.NextFeature();
                        bool foundMatch = false;

                        // Check if a segment was found in another quad with the same characteristics.
                        if (possibleCandidateFeature != null)
                        {

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

                                    // Asign the high and low numbers (make sure the low is not zero)
                                    if (fromAddrL == 0 || fromAddrR == 0)
                                    {
                                        if (fromAddrL == 0)
                                        {
                                            lowNum = fromAddrR;
                                        }
                                        else
                                        {
                                            lowNum = fromAddrL;
                                        }
                                    }
                                    else
                                    {
                                        if (fromAddrL < fromAddrR)
                                        {
                                            lowNum = fromAddrL;
                                        }
                                        else
                                        {
                                            lowNum = fromAddrR;
                                        }
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
                                    var fitsInLargerSegmentFilter = new QueryFilter
                                    {
                                        WhereClause = @"OBJECTID = " + possibleCandidateOID + @" AND 
                                                        (((" + lowNum + @" BETWEEN FROMADDR_L  AND TOADDR_L ) or (" + highNum + @" BETWEEN FROMADDR_L  AND TOADDR_L)) or 
                                                        ((" + lowNum + @" BETWEEN FROMADDR_R  AND TOADDR_R ) or (" + highNum + @"  BETWEEN FROMADDR_R  AND TOADDR_R)))"
                                    };

                                    //Console.WriteLine("for oid: " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("OBJECTID")).ToString() + " " +  fitsInLargerSegmentFilter.WhereClause.ToString());

                                    var fitsInLargerSegmentFeatureCursor = geocodeRoads.Search(fitsInLargerSegmentFilter, false);
                                    comReleaser3.ManageLifetime(fitsInLargerSegmentFeatureCursor);

                                    // Check if matching seg was found.
                                    var fitsInLargerFeature = fitsInLargerSegmentFeatureCursor.NextFeature();

                                    if (fitsInLargerFeature != null)
                                    {
                                        // A match was found.  So don't write it to the AltNames table
                                        consoleCounter = consoleCounter + 1;
                                        Console.WriteLine(consoleCounter + ": fit in larger: " + fitsInLargerFeature.get_Value(fitsInLargerFeature.Fields.FindField("OBJECTID")).ToString());
                                        foundMatch = true;
                                        break;
                                    }
                                    else
                                    {
                                        // the current segment's address range did not fit within a larger segment
                                        // check if a smaller address ranged segment fits into this larger segment
                                        using (var comReleaser4 = new ComReleaser())
                                        {
                                            // Get the lowest and the highest number of the possible candidate
                                            int geocodeRoadOID_ = Convert.ToInt32(geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("OBJECTID")));
                                            int fromAddrL_ = Convert.ToInt32(possibleCandidateFeature.get_Value(possibleCandidateFeature.Fields.FindField("FROMADDR_L")));
                                            int fromAddrR_ = Convert.ToInt32(possibleCandidateFeature.get_Value(possibleCandidateFeature.Fields.FindField("FROMADDR_R")));
                                            int toAddrL_ = Convert.ToInt32(possibleCandidateFeature.get_Value(possibleCandidateFeature.Fields.FindField("TOADDR_L")));
                                            int toAddrR_ = Convert.ToInt32(possibleCandidateFeature.get_Value(possibleCandidateFeature.Fields.FindField("TOADDR_R")));
                                            int highNum_;
                                            int lowNum_;

                                            // Asign the high and low numbers (make sure the low is not zero)
                                            if (fromAddrL_ == 0 || fromAddrR_ == 0)
                                            {
                                                if (fromAddrL_ == 0)
                                                {
                                                    lowNum_ = fromAddrR_;
                                                }
                                                else
                                                {
                                                    lowNum_ = fromAddrL_;
                                                }
                                            }
                                            else
                                            {
                                                if (fromAddrL_ < fromAddrR_)
                                                {
                                                    lowNum_ = fromAddrL_;
                                                }
                                                else
                                                {
                                                    lowNum_ = fromAddrR_;
                                                }
                                            }

                                            if (toAddrL_ > toAddrR_)
                                            {
                                                highNum_ = toAddrL_;
                                            }
                                            else
                                            {
                                                highNum_ = toAddrR_;
                                            }


                                            var smallerCandidateFitsInFilter = new QueryFilter
                                            {
                                                WhereClause = @"OBJECTID = " + geocodeRoadOID_ + @" AND 
                                                        (((" + lowNum_ + @" BETWEEN FROMADDR_L  AND TOADDR_L ) or (" + highNum_ + @" BETWEEN FROMADDR_L  AND TOADDR_L)) or 
                                                        ((" + lowNum_ + @" BETWEEN FROMADDR_R  AND TOADDR_R ) or (" + highNum_ + @"  BETWEEN FROMADDR_R  AND TOADDR_R)))"
                                            };

                                            var smallerCandidateFitsInFeatureCursor = geocodeRoads.Search(smallerCandidateFitsInFilter, false);
                                            comReleaser4.ManageLifetime(smallerCandidateFitsInFeatureCursor);

                                            // Check if matching seg was found.
                                            var smallerCandidateFitsInFeature = smallerCandidateFitsInFeatureCursor.NextFeature();

                                            if (smallerCandidateFitsInFeature != null)
                                            {
                                                // A match was found.  So don't write it to the AltNames table
                                                consoleCounter = consoleCounter + 1;
                                                Console.WriteLine(consoleCounter + ": candidate fit within it: " + smallerCandidateFitsInFeature.get_Value(smallerCandidateFitsInFeature.Fields.FindField("OBJECTID")).ToString());
                                                foundMatch = true;
                                                break;
                                            }
                                        }
                                    }
                                }

                                // Advance to the next feature in the cursor.
                                possibleCandidateFeature = possibleCandidatesFeatureCursor.NextFeature();
                            }
                        }
                        else
                        {
                            // No possible candidates were found in other quad, so add this segment to the AltNames table
                        }

                        // Write to the AltNames table > match was not found
                        if (foundMatch == false)
                        {
                            // A matching feature was not found.
                            // Add a record to the table without a predir.
                            // Remove the PREDIR value from the field map
                            var valueMapNewSchemaNoPredir = valueMapNewSchema;
                            valueMapNewSchemaNoPredir.Remove("PREDIR");
                            EsriHelper.InsertRowInto(geocodeRoadFeature, altnameTableRoads, valueMapNewSchema);

                            consoleCounter = consoleCounter + 1;
                            Console.WriteLine(consoleCounter + ": not found in the other grid: " + geocodeRoadFeature.get_Value(geocodeRoadFeature.Fields.FindField("OBJECTID")).ToString());
                        }
                    }
                }
                // stop editing from altnames road table (this allow the roads altnames table to be preserved even if the altnames for addr pnts (below) errors out)
                outputEditWorkspace.StopEditOperation();
                outputEditWorkspace.StopEditing(true);
            }
        }
    }
}
