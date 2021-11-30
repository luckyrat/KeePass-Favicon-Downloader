KeePass Favicon Downloader plugin
=================================

Version 1.9.0 changes
---------------------
- Improved website downloading code to increase compatibility with unusual web sites
(Thanks to @incognito1234 for the CookieContainer tip and @kuc RE multiple link@rel=icon elements)

Pre-requisites
--------------
KeePass Password Safe 2.09+ (latest version tested only on 2.27)

Installation instructions
-------------------------
Put the .plgx file into a folder called "plugins" inside your 
KeePass Password Safe installation folder 
(often C:\Program Files\KeePass Password Safe 2\)

#### Chocolatey 📦 
Or you can [use Chocolatey to install](https://community.chocolatey.org/packages/keepass-plugin-favicon#install) it in a more automated manner:

```
choco install keepass-plugin-favicon
```

To [upgrade KeePass Plugin Favicon Downloader](https://community.chocolatey.org/packages/keepass-plugin-favicon#upgrade) to the [latest release version](https://community.chocolatey.org/packages/keepass-plugin-favicon#versionhistory) for enjoying the newest features, run the following command from the command line or from PowerShell:

```
choco upgrade keepass-plugin-favicon
```

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

Note that in both siuations mentioned above, un-necessary icons can also be
manually removed by using the standard KeePass "choose icons" dialog.

Support
-------
Try searching or posting on the forum: https://sourceforge.net/p/keepass-favicon/discussion/

Old changelog
=============

Version 1.8.0 changes
---------------------
- Higher quality icons are created in some circumstances
- Entries that contain URLs with no protocol (https:// or https://) can now be downloaded
- Changed website downloading code to increase compatibility with unusual web sites
- Progress form shows current entry title and username
- URL of entry added to error message
(Most improvements courtesy of @univerio and @boxmaker)

Version 1.7.2 changes
---------------------
- Protection from cyclic redirect (thanks to @DarkWanderer)

Version 1.7.1 changes
---------------------
- Update checking URL needs to be a property instead of a field

Version 1.7.0 changes
---------------------
- Entries with URLs stored in the title instead of URL field can now be processed
- Favicons that are found after a meta tag redirection can now be downloaded (thanks to @ajithhub)
- KeePass can now detect future KeePass Favicon Downloader plugin updates

Version 1.6.0 changes
---------------------
* Thanks to mausoma and psproduction for several of these improvements
- Non lower-case rel attribute values are now processed
- Favicons larger than 40KB can now be downloaded
- Favicons that are found after a redirection can now be downloaded
- Favicons with no leading / can now be downloaded
- Progress bar now displays before first (or only) favicon starts downloading
- Exact URL (page) now queried for favicon rather than domain root
- Entry's modification date is now updated (no new history entry is created though; backups before mass favicon downloading is still recommended)

Version 1.5.0 changes
---------------------
The previous favicon service provider dissapeared so I'm now using the HTMLAgilityPack to manage the whole download procedure.

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
