using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using ESRI.ArcGIS.Geodatabase;
using RoadGrinder.services;

namespace RoadGrinder.commands
{
    public static class PopulateAltNamesAddrPntsTableCommand
    {
        public static void Execute(IWorkspaceEdit outputEditWorkspace, ITable altnameTableAddrPnts, string connectionStringSgid)
        {
            // load records into the altnames table for address points
            //start editing again
            outputEditWorkspace.StartEditing(false);
            outputEditWorkspace.StartEditOperation();

            int counter = 0;

            Console.WriteLine("begin altnames table for addr pnts: " + DateTime.Now);

            const string getSgidAddrPntsPredirNotNeededQuery = @"select a.*
                        from (SELECT DISTINCT AddSystem,AddNum,AddNumSuffix,PrefixDir,StreetName,StreetType,SuffixDir,City,ZipCode,CountyID,(ltrim(rtrim(AddSystem)) + ' | ' + ltrim(rtrim(AddNum)) + ' ' + ltrim(rtrim(AddNumSuffix)) + ' ' + ltrim(rtrim(PrefixDir)) + ' ' + ltrim(rtrim(StreetName)) + ' ' + ltrim(rtrim(StreetType + ' ' + ltrim(rtrim(SuffixDir)))))UTAddPtID FROM LOCATION.ADDRESSPOINTS WHERE PrefixDir <> '' AND StreetName LIKE '%[A-Z]%' AND StreetName NOT LIKE 'HIGHWAY %') a
                        where NOT EXISTS
                        (
	                        select null
	                        from LOCATION.ADDRESSPOINTS b
	                        where a.AddSystem = b.AddSystem
	                        and a.StreetName = b.StreetName
	                        and a.AddNum = b.AddNum
	                        and a.StreetType = b.StreetType
	                        and a.SuffixDir = b.SuffixDir
	                        and a.AddNumSuffix = b.AddNumSuffix
	                        and a.PrefixDir <> b.PrefixDir
                        )";
            // old way - const string sgidAddrPntstoVerifyQuery = @"SELECT DISTINCT AddSystem,AddNum,AddNumSuffix,PrefixDir,StreetName,StreetType,SuffixDir,City,ZipCode,CountyID FROM LOCATION.ADDRESSPOINTS WHERE PrefixDir <> '' AND StreetName LIKE '%[A-Z]%' AND StreetName NOT LIKE 'HIGHWAY %' AND AddSystem = 'SALT LAKE CITY';";
            
            using (var con = new SqlConnection(connectionStringSgid))
            {
                con.Open();
                var getSgidAddrPntsPredirNotNeeded = con.Query(getSgidAddrPntsPredirNotNeededQuery);
                //old way - var sgidAddrPntsToVerifyList = con.Query(sgidAddrPntstoVerifyQuery);

                // loop through potential altname address points (from sgid records) 
                var sgidAddrPntsResults = getSgidAddrPntsPredirNotNeeded as dynamic[] ?? getSgidAddrPntsPredirNotNeeded.ToArray();
                foreach (var sgidAddrPnt in sgidAddrPntsResults)
                {
                    // add the addresspoint to the altnames table
                    var dictRow = sgidAddrPnt as IDictionary<string, object>;
                    
                    // Remove the PREDIR key/value from the Dictionary.
                    dictRow.Remove("PrefixDir");

                    // replace the possible double space in the UTAddPtID field from missing AddNumSuffix
                    // get the value from the field, and trim it
                    string stringRemovedDblSpace = dictRow["UTAddPtID"].ToString();
                    //if (stringRemovedDblSpace != null)
                    //{
                    //    stringRemovedDblSpace = stringRemovedDblSpace.Trim();                        
                    //}
                    stringRemovedDblSpace = stringRemovedDblSpace.Trim();
                    stringRemovedDblSpace = Regex.Replace(stringRemovedDblSpace, @"\s+", " ");
                    if (stringRemovedDblSpace != dictRow["UTAddPtID"].ToString())
                    {
                        dictRow.Remove("UTAddPtID");
                        dictRow.Add("UTAddPtID", stringRemovedDblSpace);                        
                    }

                    // Insert these values into the esri fgdb table.
                    EsriHelper.InsertRowInto(altnameTableAddrPnts, dictRow);
                }
                
                
                //var sgidAddrPntsList = sgidAddrPntsToVerifyList as dynamic[] ?? sgidAddrPntsToVerifyList.ToArray();
               
                //foreach (var sgidAddrPntToVerify in sgidAddrPntsList)
                //{
                //    // check for matching address point in same address-system with different prefix
                //    // Check is the SuffixDir is empty or null before creating the query
                //    string matchingAddrPntQuery = @"SELECT Count(*) Count FROM LOCATION.ADDRESSPOINTS WHERE AddSystem = '" + sgidAddrPntToVerify.AddSystem + @"' AND AddNum = '" + sgidAddrPntToVerify.AddNum + @"' AND PrefixDir <> '" + sgidAddrPntToVerify.PrefixDir + @"' AND StreetName = '" + sgidAddrPntToVerify.StreetName + @"' AND StreetType = '" + sgidAddrPntToVerify.StreetType + @"' AND SuffixDir = '" + sgidAddrPntToVerify.SuffixDir + @"' AND AddNumSuffix = '" + sgidAddrPntToVerify.AddNumSuffix + @"';";

                //    using (var con1 = new SqlConnection(connectionStringSgid))
                //    {
                //        con1.Open();
                //        var rowCount = con1.ExecuteScalar<int>(matchingAddrPntQuery);

                //        if (rowCount == 0)
                //        {
                //            counter = counter + 1;
                //            Console.WriteLine(counter + ": AddressPoints: not found with other predir.");
                //            IDictionary<string, object> dictRow =
                //                sgidAddrPntToVerify as IDictionary<string, object>;
                //            //dictRow.Remove("OBJECTID");
                //            dictRow.Remove("PrefixDir");
                //            EsriHelper.InsertRowInto(altnameTableAddrPnts, dictRow);
                //        }
                //        else
                //        {
                //            Console.WriteLine(counter + ": AddressPoints: found with other predir - don't add to altnames table.");
                //        }
                //    }
                //}

                // stop editing from altnames addr pnts table
                outputEditWorkspace.StopEditOperation();
                outputEditWorkspace.StopEditing(true);
            }
        }
    }
}
