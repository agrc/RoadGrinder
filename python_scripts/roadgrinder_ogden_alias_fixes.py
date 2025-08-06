# -*- coding: utf-8 -*-
"""
Created on Fri Jul 11 14:20:21 2025
@author: eneemann
Script to remove Ogden aliases from the RoadGrinder address point alias table
"""

import arcpy
import time

# start timer and print start time
start_time = time.time()
readable_start = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())
print("The script start time is {}".format(readable_start))
today = time.strftime("%Y%m%d")

#: set up variables
addpt_alias_table = r'L:\roadgrinder_data\RoadGrinder.gdb\AtlNamesAddrPnts'

#: get initial count
initial_alias_count = int(arcpy.management.GetCount(addpt_alias_table)[0])
print(f"Initial alias table row count: {initial_alias_count}")

#: make layer for selection (table view)
where_clause = """AddSystem = 'OGDEN' And City IN ('OGDEN CITY', '')"""
arcpy.management.MakeTableView(addpt_alias_table, "alias_selection", where_clause)

#: select Ogden address point aliases to remove
arcpy.management.SelectLayerByAttribute("alias_selection", "NEW_SELECTION", where_clause)

#: get the count of selected rows
selected_count = int(arcpy.management.GetCount("alias_selection")[0])
print(f"Number of rows selected: {selected_count}")

#: delete selected rows, if > 0
if selected_count > 0:
    print("Deleting selected rows")
    arcpy.management.DeleteRows("alias_selection")
else:
    print("No rows to delete")

#: get final count
final_alias_count = int(arcpy.management.GetCount(addpt_alias_table)[0])
print(f"Final alias table row count: {final_alias_count}")


print("Script shutting down ...")
#: stop timer and print end time
readable_end = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())
print("The script end time is {}".format(readable_end))
print("Time elapsed: {:.2f}s".format(time.time() - start_time))