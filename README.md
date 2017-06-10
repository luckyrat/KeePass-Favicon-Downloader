# KeePass Favicon Downloader plugin

## Installation

### Windows

1. Install KeePass Password Safe 2.09+ (latest version tested only on 2.36).
2. Put the `.plgx` file into a folder called "Plugins" inside your KeePass Password Safe installation folder (often C:\Program Files (x86)\KeePass Password Safe 2\)

## Linux

Just install the package `keepass2-plugin-favicondownloader.deb`.

## Known issues

**UPDATE:** Both known issues below can now be worked around using the [Custom Icon Dashboarder](https://sourceforge.net/projects/keepasscustomicondashboarder/)
plugin by @incognito1234.

1. Sharing a database between 32bit and 64bit machines
may result in duplicate favicons being stored in the database.
This shouldn't have a serious impact but is a bit wasteful so
I would like to improve this one day.

2. Similar issue when favicons change - there is no automatic method to track
which favicons are actually in use so old icons will stay orphaned in the database.

**Note:** In both situations mentioned above, unnecessary icons can also be
manually removed by using the standard KeePass "choose icons" dialog.

## Support
Try searching or posting on the [forum](https://sourceforge.net/p/keepass-favicon/discussion/).

## Building

**Note:** _Debug_ creates a `.dll` while _Release_ creates a `.plgx`.

### Windows

1. Open `src/KeePassFaviconDownloader.sln` in Visual Studio
2. Rebuild all

### Linux

Make Release: `make`

Make Debug: `make -f MakefileDebug`

Build Debian Package: `dpkg-buildpackage -b -rfakeroot -us -uc`
