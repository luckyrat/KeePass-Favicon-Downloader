KeePass Favicon Downloader plugin
=================================

Pre-requisites
--------------
KeePass Password Safe 2.09+ (latest version tested only on 2.27)

Installation instructions
-------------------------
Put the .plgx file into a folder called "plugins" inside your 
KeePass Password Safe installation folder 
(often C:\Program Files\KeePass Password Safe 2\)

Known issues
------------

UPDATE 2014-07-03: Both known issues below can now be worked around using the "Custom Icon Dashboarder"
plugin by @incognito1234 available at https://sourceforge.net/projects/keepasscustomicondashboarder/

1) Sharing a database between 32bit and 64bit machines
may result in duplicate favicons being stored in the database.
This shouldn't have a serious impact but is a bit wasteful so
I would like to improve this one day.

2) Similar issue when favicons change - there is no automatic method to track
which favicons are actually in use so old icons will stay orphaned in the database.

Note that in both situations mentioned above, unnecessary icons can also be
manually removed by using the standard KeePass "choose icons" dialog.

Support
-------
Try searching or posting on the forum: https://sourceforge.net/p/keepass-favicon/discussion/
