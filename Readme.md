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
Open the solution in the version Visual Studio supported by
your version of ArcGIS. Select Build Solution from the VS menu.

Extra caution should be made to thoroughly test any changes
before deploying to ensure that the changes are stable. MapFixer
runs whenever ArcMap is started for almost all Alaska GIS users.
It would be very frustrating if it crashes ArcMap.

A sample moves database is in `verifier/TestMoves.csv`

## Deploy

### Deploy MapFixer

After building a release version, provide the file
`MapFixer/bin/release/MapFixer.esriAddin` to the PDS data manager
and ask her to install it in `X:\GIS\Addins\10.X` where it will
automatically be loaded by most Alaska GIS users. (replace `10.x` by
all versions of ArcGIS greater than or equal to the version set in
[`MapFixer/config.esriaddinx`](https://github.com/AKROGIS/MapFixer/blob/1c3f6ef5755796bc97cb9cde882574e824c22a33/MapFixer/Config.esriaddinx#L11).)
Users without network access can get a copy from someone who does,
and then double click the Addin file to launch the esri Addin
install tool.

### Deploy Verifier

After building a release version, provide the files
`MapFixer/bin/release/MapFixer.dll` and
`verifier/bin/release/verifier.exe` to the PDS data manager
and ask her to install it in `\GIS\Tools\MovesDbVerifier\10.x`.
Replace `10.x` with the version of ArcGIS installed on the
computer that did the build.

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
`X:\GIS\Addins\10.x` per our installation instructions and
they are on the NPS network or have an external hard drive
attached as their X drive). It runs at start up and whenever
a new map document is loaded.  It will only present itself
to the user if a broken data sources is found in the map.

It can be run manually with the following instructions:

* In ArcMap Choose `Customize -> Customize Mode...` from the menu.
* Choose the `Commands` tab.
* In the `Categories:` list, scroll down and select `NPS Alaska`
* In the `Commands:` list, scroll down and select `Fix This Map`.
  Drag the icon and drop it onto an existing toolbar.
* Close the customize window.
* Click the `Fix This Map` button to run the tool.

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
$ cd \GIS\Tools\MovesDbVerifier\10.6
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
