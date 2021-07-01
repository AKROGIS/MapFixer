# Map Fixer

An ArcMap 10.x Addin for fixing broken datasources in an
ArcMap document when the breakage is due to a known data
move on the AKRO GIS data server (PDS aka X Drive).

The tool reads a list of known data moves/renames from a
database (DSV spreadsheet) on the PDS to know which
broken links it can fix.

Since the tool runs automatically and silently whenever a
new ArcMap document is opened, it is very important that it
not crash ArcMap.  To help avoid that nightmare scenario,
There is a related console tool to verify that the
database is well formed. This tool should be run before
changes to the database are published to the PDS.

## Build

Install the ArcObjects SDK (comes with ArcGIS Desktop 10.x)
if building the ArcMap version, and/or the
[Pro SDK for .Net](https://github.com/Esri/arcgis-pro-sdk/wiki/ProGuide-Installation-and-Upgrade)
if building the ArcGIS Pro version.
Open the solution in the version Visual Studio supported by
your version of ArcGIS. Select Build Solution from the VS menu.

## Testing

Extra caution should be made to thoroughly test any changes
before deploying to ensure that the changes are stable. MapFixer
runs whenever ArcMap is started for almost all Alaska GIS users.
It would be very frustrating if it crashes ArcGIS.

A sample moves database is in `verifier/TestMoves.csv`.  For NPS
developers, there are test ArcMap documents and Pro layer files at
`T:\Users\Regan\Other Stuff\Testing Data\MapFixer`. These files
are broken in ways that should be fixed with the moves database
deployed to the PDS.

## Deploy

### Deploy MapFixer

After building a release version, provide the files
`MapFixer/Map_Addin/bin/release/MapFixer.esriAddin` and/or
`MapFixer/Pro_Addin/bin/release/MapFixer.esriAddinx`
to the PDS data manager
and ask her to install them in `X:\GIS\Addins\10.X` and/or
`X:\GIS\Addins\Pro` respectively.  Addins in these locations will
automatically be loaded by most Alaska GIS users. (replace `10.x` by
all versions of ArcGIS greater than or equal to the version set in
[`MapFixer/Map_Addin/config.esriaddinx`](https://github.com/AKROGIS/MapFixer/blob/4803e5ab7e99645623d0fb5e37c85add3f0785bb/Map_AddIn/Config.esriaddinx#L11).)
Users without network access can get a copy from someone who does,
and then double click the Addin file to launch the esri Addin
install tool.

### Deploy Verifier

After building a release version, provide the files
`verifier/bin/release/MovesDatabase.dll` and
`verifier/bin/release/verifier.exe` to the PDS data manager
and ask her to install them in `\GIS\Tools\MovesDbVerifier`.
The verifier does not require any version of ArcGIS to
verify the moves database.

### Deploy the Moves Database

The master copy of the moves data base is in a spreadsheet
maintained by the PDS data manager and is updated whenever
data in the PDS is moved, renamed, or deleted (any change to
the PDS that might cause an existing map to break.)
The PDS manager will export the spreadsheet to a CSV file,
and then do a global search, replacing the commas (`,`) with
pipes (`|`).  The PDS manager will then verify the CSV file
(see Using Verifier below) and fix all issues before
publishing the file to `X:\GIS\ThemeMgr\DataMoves.csv`

## Using

### Using MapFixer

The MapFixer addin will run automatically for most Alaska GIS
Users (so long as they have their Addin folder set to
`X:\GIS\Addins\10.x` (for ArcMap) or `X:\GIS\Addins\Pro`
(for ArcGIS Pro) per our installation instructions and
they are on the NPS network or have an external hard drive
attached as their X drive). It runs at start up and whenever
a new map document is loaded.  It will only present itself
to the user if a broken data sources is found in the map.

It can be run manually with the following instructions:


* In ArcMap
  * Choose `Customize -> Customize Mode...` from the menu.
  * Choose the `Commands` tab.
  * In the `Categories:` list, scroll down and select `NPS Alaska`
  * In the `Commands:` list, scroll down and select `Fix This Map`.
    Drag the icon and drop it onto an existing toolbar.
  * Close the customize window.
  * Click the `Fix This Map` button to run the tool.
* In Pro
  * Click on the Add-In tab in the ribbon.
  * Click the `Check Map` button in the Map Fixer
    group in the Add-In ribbon.

### Using Verifier

The verifier tool is run from a CMD (DOS) Window or a
PowerShell.  It is run by the PDS data manager to
check changes to the moves database _before_ they are
published to the PDS.

1) Start the `CMD` or `PowerShell` application.
2) Change the working directory to match the version of
   ArcGIS installed on your computer

```sh
$ x:
$ cd \GIS\Tools\MovesDbVerifier
```

3) Run the command with the file to be checked. With no
filename provided it will check the master database at
`X:\GIS\ThemeMgr\DataMoves.csv`

```sh
$ .\verifier.exe C:\tmp\newmoves.csv
```
or
```sh
$ .\verifier.exe
```
