KeePass Favicon Downloader plugin
=================================

Version 1.2 changes
-------------------
Most download errors should now be displayed to the user rather than crashing KeePass.

Version 1.1 changes
-------------------
URLs without http:// in front of them are now processed.

Version 1.0 release notes
-------------------------

Pre-requisites
--------------
KeePass Password Safe 2.09

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

3) Favicons for some websites are not downloaded at the first attempt. This
problem is usually resolved automatically within a few days so just try to
download the favicons for the affected websites again after waiting for
a little while.

Support
-------
Try searching or posting on the forum: https://sourceforge.net/projects/keepass-favicon/forums