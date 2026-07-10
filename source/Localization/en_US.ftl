# Generic
Authentication = Authentication
Options = Options
Mappings = Emulator Mappings
Saves = Saves
States = Save States
Browse = Browse
Installation = Installation
Refresh = Refresh
Cancel = Cancel
AreYouSure = Are you sure?
Remote = Remote
Local = Local
LastModified = Last Modified
Game = Game
LocalFiles = Local Files
File = File
Status = Status
Sync = Sync
Enabled = Enabled
EnabledQuestion = Enabled?
Dismiss = Dismiss

# Plugin Main
InstallFailed = Failed to install - {$Error}
OpenRomMLibrary = Open RomM library
OpenRomMProfile = Open RomM profile


# Authentication Page
AuthButton = Connect
ServerText = RomM Server Address
ClientToken = Client Token
UseBasicAuth = Use Basic Auth
UserPassWarning = Warning! Using basic login is NOT recommended!
Username = Username
Password = Password
InvalidScheme = Host address must start with http or https
CustomHeaders = Custom Headers
CustomHeaderMalformed = Custom header doesn't contain both a Name and Value!
AdvanceOptions = Advanced Options
NewHeader = New Header
Login = Login
EnableBasicAuth = Basic Auth not enabled, cannont login with username/password
LoginNoToken = Client token is empty, cannot login!
LoginWithToken = Login with Client Token

# Mappings page
SyncPlatforms = Sync Platforms
PlatformsSynced = Synced {$PlatformCount} platforms
NewMapping = New Mapping
Emulator = Emulator
Profile = Profile
Platform = Platform
ROMLoc = ROM Location
AutoExtractROMs = Automatically Extract Archived ROMs
PreferM3U = Prefer .m3u Files When Launching
MappingEnabled = Enabled
AutoExtractROMsTooltip = Will extract ROMs if they are stored in RAR, Zip, Tar, 7Zip, GZip, Arc, Arj, Ace or Lzw format!
PreferM3UTooltip = Will use .m3u file if multi-file ROM and emulator supports .m3u!
SaveOptions = Save Options
AutoSaveDetection = Auto Save Detection
AutoSaveDetectionTooltip =
    Detects files that share the same name as the ROM filename

    - Single File: Looks for a single save file of a set extension
        e.g. Mario Kart DS (Europe).sav
    - Fixed Set: Looks for all files that match the extensions set
        e.g. Pokemon FireRed (Europe).sav
        Pokemon FireRed (Europe).rtc
    - Folder: Looks for a folder name that matches
        e.g. {"{"}SaveDirectory{"}"}/Final Fantasy VII (Europe)/
SaveExtensions = Save Extensions
SaveExtensionsPlaceholder = srm;sav;gci
SaveLocation = Save Location
ManageSaves = Manage Saves
ManageSaveStates = Manage Save States

# Options page
LibraryScanning = Library Scanning
MergeROMs = Merge ROM revisions
KeepDeleted = Keep Games Deleted from the RomM Server
SkipDeleted = Skip Importing ROMs Missing from the RomM Server's File System
ExcludeGenres = Exclude Genres
Use7z = Use 7z for archive extraction
StatusSync = Status Sync
KeepStatusSynced = Keep completion status in sync with RomM
KeepFavouritesSynced = Keep favorites in sync with RomM
KeepPrivateNotesSynced = Keep private notes in sync with RomM
KeepPublicNotesSynced = Keep public notes in sync with RomM
SaveSync = Save Sync
EnableSaveSyncing = Enable Save Syncing
DownloadSaveOnLaunch = Download save on game launch
UploadSaveOnFinished = Upload save after game finished
SaveConflictsLabel = Save conflicts
AutoCleanOldSaves = Auto-clean old saves on server, keeping the newest
SaveStateSync = Save State Sync
EnableSaveStateSyncing = Enable Save State Syncing
ExcludeGenresPlaceholder = Adventure;Platform;RPG;

# Http Client
Reauthenticate = Reauthentication required!
GETFailed = GET Request Failed For {$APIPath}
POSTFailed = POST Request Failed For {$APIPath}
PUTFailed = PUT Request Failed For {$APIPath}
DELETEFailed = DELETE Request Failed For {$APIPath}

# Import
NoFileNameWithID = The filename for ROM ID {$ROMID} does not exist. Does the ROM exist on the server's file system?
ROMImportFailed = Failed to import {$GameName} [ID:{ROMID}], Skipping
ROMDataSaveFailed = Failed to save ROM data to disk - {$Error}

# Import controller
NoEmulatorsConfigured = No emulators are configured or enabled in RomM settings. No games will be imported.
PlatformNotFound = Platform {$PlatformName} (ID: {$PlatformID}) was not found in RomM. Skipping.
DownloadROMDataFailed = Failed to download ROMs for {$PlatformName}: {$Error}

# Account
NewProfileIconFailed = Failed to upload new profile image
ClientTokenAddressFailed = Cannot open the client token page because the RomM server address is not set.
HeartbeatFailed = Server heartbeat request failed.
HostNotSet = RomM host is not configured. Set it in the settings
HostInvalid = RomM host is invalid. Please check the URL in the settings.
UserPassNotSet = Cannot log in because the username or password is not set.
TokenNotSet = Cannot log in because the client token is not set.
LoginSuccess = Login Successful!
NotAuthenticated = User is not authenticated. Please Log in.
GETProfileIconFailed = Failed to get profile icon - {$Error}
GETDevicesFailed = Failed to get RomM devices - {$Error}
CreateNewDeviceFailed = Failed to create new device - {$Error}
FavouritesUpdateFailed = Can't update favorites, collection is null
NoPlatforms = No platforms retrieved from server!
FailedQRSetup = Failed to set up QR code - {$Error}
FailedServerPair = Failed to pair with server - {$Error}
PairWasNull = Response was null
PairExpired = Expired
PairWasDenied = Request was denied
CreateFavoritesFailed = Failed to create favorites collection

# Settings
SettingSaveFailed = Failed to save settings - {$Error}
SettingLoadFailed = Failed to load settings - {$Error}

# Downloads
DownloadViewName = RomM Downloads
DownloadViewTitle = Downloads
DownloadFailed = Failed to download {$GameName} - {$Error}

# Status Controller
LibraryIdConvertFailed = Failed to parse {$GameID}, Skipping task!
CompletionStatusNameFailed = Failed to get name of completion status
ConvertStatusFailed = {$PlayniteStatus} cannot be converted to a RomM status
GameHeartbeatFailed = Failed to send activity heartbeat 

# Save Controller
FailedGetSaves = Failed to get local saves - {$Error}
FailedUploadSaves = Failed to get upload save - {$Error}
FailedNegotiateSaves = Failed to negotiate save - {$Error}
WantKeepSave = Which save do you want to keep?
SaveConflict = Save Conflict!
UseRemote = Use Remote
UseLocal = Use Local
Skip = Skip
DownloadedSave = Downloaded save - {$SavePath} ({$Bytes})
UploadedSave = Uploaded save - {$SavePath} ({$Bytes})
SaveArchiveNotFound = Save archive not found at {$SaveLoc}, aborting!
ArchiveResolvesOutside = Archive entry '{$Entry}' resolves outside destination, aborting
ExtractionEmpty = Extraction reported success but archive is empty
FailedUnpack = Failed to unpack save archive at {$SaveLoc}
SaveStatusUnknown = Unknown
SaveStatusLocalNewer = Local Newer
SaveStatusRemoteNewer = Remote Newer
SaveStatusConflict = Conflict
SaveStatusSynced = Synced

# Mapping Saves
EnableAllSaves = Enable All Saves
SaveManagerTitle = Save Manager
LocalSaves = Local Saves
RemoteSaves = Remote Saves
AddManualSave = Add manual save
UnsyncedAutoDetectSaves = Unsynced Auto-Detected Saves
NoSavePathSet = Save Path in mapping not set cannot download save!
RemoveEntry = Remove entry
DeleteSaveLocal = Delete save (local only)
DeleteSaveBoth = Delete save completely
SaveNoGameSelected = No game was selected cannot create save backup
SaveNoFilesSelected = No save files/folders were selected cannot create save backup
FilterByGameName = Filter by game name
NoSlotWarning = Save has no slot, a copy of this save will be created when syncing!
EmptyLocalSaves = No local saves found for this platform yet
EmptyRemoteSaves = No remote saves found for this platform yet — click Refresh.
EmptyUnmatchedSaves = All possible auto detected files are already matched for this platform
SelectGame = Select Game
SyncNewSave = Sync New Save
SelectSaveFilesInPath = Select save files/folders in {$SavePath}

# Mapping Saves Messagebox
DeleteSaveTitle = Delete Save? 
DeleteSaveMessage = How do you want to delete the save?
UploadSaveTitle = Upload Save? 
UploadSaveMessage = Do you want to upload this save?
DownloadSaveTitle = Download Save?
DownloadSaveMessage = Do you want to download this save?\nSave: {$SaveName}\nSave Path: {$SavePath}'?
DeleteMappingConfirmTitle = Are you sure you want to delete this mapping?

# Single Game Save Tab
SaveConflictBannerText = Local and remote saves differ.
KeepLocalUpload = Keep Local (upload)
KeepRemoteDownload = Keep Remote (download)
ManualPickHint = Select the file(s) that belong to this game's save, then click Set as Save.
SetAsSave = Set as Save
SyncEnabled = Sync enabled
UploadNewSave = Upload New Save

# Enum descriptions
SaveConflictStyle_Ask = Ask
SaveConflictStyle_PreferNewer = Prefer Newer
SaveConflictStyle_PreferRemote = Prefer Remote
SaveConflictStyle_PreferLocal = Prefer Local
    ## See AutoSaveDetectionTooltip key to understand what these mean
SaveLayoutStyle_SingleFile = Single File
SaveLayoutStyle_FixedSet = Fixed Set
SaveLayoutStyle_WholeFolder = Folder
SaveLayoutStyle_Disabled = Disabled

# Installing
DownloadStatusQueued = Queued
DownloadStatusCompleted = Completed
DownloadStatusCanceled = Canceled
DownloadStatusFailed = Failed
DownloadStatusDownloading = Downloading...
DownloadStatusDownloadingPct = Downloading... {$Percent}%
DownloadStatusExtracting = Extracting...
DownloadStatusExtractingPct = Extracting... {$Percent}%
UninstallFailed = Failed to delete ROM from filesystem, Marking as uninstalled - {$Error}