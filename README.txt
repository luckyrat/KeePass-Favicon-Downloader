KeePass Favicon Downloader plugin
=================================

Version 1.5.0 changes
---------------------
The previous favicon service provider dissapeared so I'm now using the HTMLAgilityPack to manage the whole download procedure.

Pre-requisites
--------------
KeePass Password Safe 2.09+ (latest version tested only on 2.12)

Installation instructions
-------------------------
Put the .plgx file into a folder called "plugins" inside your 
KeePass Password Safe installation folder 
(often C:\Program Files\KeePass Password Safe 2\)

Known issues
------------
1) Sharing a database between 32bit and 64bit machines
may result in duplicate favicons being stored in the database.
This shouldn't have a serious impact but is a bit wasteful so
I would like to improve this one day.

2) Similar issue when favicons change - there is no automatic method to track
which favicons are actually in use so old icons will stay orphaned in the database.

Note that in both siuations mentioned above, un-necessary icons can be
manually removed by using the standard KeePass "choose icons" dialog.

Support
-------
Try searching or posting on the forum: https://sourceforge.net/projects/keepass-favicon/forums

Old changelog
=============

Version 1.4.1 changes
-------------------
It will hopefully work correctly on .NET 2.0 now.

Version 1.4 changes
-------------------
If the webservice fails or returns a "default/unknown" icon then we now try grabbing it directly from the target website. It will only work if the icon is in the traditional location (/favicon.ico).
Only the last error message is displayed now (a future improvement might be to log all errors somewhere but for now, this at least avoids having to click through hundreds of error messages if the favicon webservice is unavailable during bulk favicon downloads).

Version 1.3 changes
-------------------
KeePass tray icon re-enabled after Favicons have been downloaded.
Fixed a bug where some KeePass features stopped working after downloading favicons (e.g. autotype)

Version 1.2 changes
-------------------
Most download errors should now be displayed to the user rather than crashing KeePass.

Version 1.1 changes
-------------------
URLs without http:// in front of them are now processed.