# Generic
Authentication = Authentication
Options = Options
Mappings = Emulator Mappings
Saves = Saves
States = Save States
Browse = Browse
Back = Back
Installation = Installation
Refresh = Refresh
Cancel = Cancel
AreYouSure = Are you sure?
Remote = Remote
Local = Local
LastModified = Last Modified
Game = Game
Name = Name
Value = Value
LocalFiles = Local Files
File = File
Status = Status
Sync = Sync
Enabled = Enabled
EnabledQuestion = Enabled?
Dismiss = Dismiss
Start = Start
NoFolderPlaceholder = No Folder Selected
NoExePlaceholder = Executable Not Set
Disabled = Disabled
Download = Download
FilePaths = File Paths
SlotLabel = Slot
SyncTooltip = Sync
SyncUploadTooltip = Sync (Upload)
SyncDownloadTooltip = Sync (Download)
ResolveConflictTooltip = Resolve Conflict
StartTrackingDownloadTooltip = Start tracking (Download)
StartTrackingUploadTooltip = Start tracking (Upload)
UploadRestoredSaveTooltip = Upload restored save
ResolveMissingFilesTooltip = Resolve missing files
AddFileTooltip = Add file
AddFolderTooltip = Add folder
FolderLabel = Folder
RemoveTooltip = Remove
History = History
Current = Current
SwitchToThis = Switch to this
NoSaveHistory = No save history
ManualSyncLabel = Manual sync:
ForceUpload = Force Upload
ForceUploadTooltip = Uploads the local save, overwriting whatever is on the server
ForceDownload = Force Download
ForceDownloadTooltip = Downloads the server save, overwriting the local copy
DuplicateToArchive = Duplicate to archive
DuplicateToArchiveTooltip = Makes a permanent copy of the save that future syncs and auto-cleanup will never overwrite or delete

# Save Management dialogs
RestoreLocallyOnly = Restore Locally Only
RestoreAndSync = Restore & Sync
UntrackSaveButton = Untrack Save
NoROMsForMapping = No ROMs found for this mapping, cannot create new save
SaveAlreadySynced = Save is already in sync!
UploadRestoredSaveConfirm = How do you want to upload restored save?
UploadRestoredSaveTitle = Upload Restored Save
MissingFilesConfirm = Some files/folders were missing on last sync, Do you want to stop tracking these paths?{$Paths}
RestoreHistoricSaveTitle = Restore Historic Save
SaveStatusUnknownWarning = Save status is unknown!, skipping
ROMNotFoundForSave = Failed to find ROM for this save, skipping
ParentSaveNotFound = Failed to find parent save, skipping
RestoreHistoricSaveConfirm = How do you want to restore the historic save?
GameNotFoundForSave = Couldn't find Game with this save, skipping
MappingNotFoundForGame = Couldn't find mapping that goes with this game, skipping
FilesOutsideMappingDir = One or more files/folders were not in the mapping save directory they have been skipped

# Save Manager
SyncCannotStart = Cannot do save sync operations as a game is currently running!
UploadROMNotFound = Failed to find ROM that matches save, skipping upload
UploadMappingNotFound = Failed to find mapping, skipping upload
UploadPackFailed = Failed to pack save, skipping upload
UploadPathsSkipped = One or more paths were skipped when packing save, skipping upload
UploadFilesMissing = Save files are missing, skipping upload
UploadConflictResolveFailed = Failed to resolve save conflict, skipping upload
UploadServerFailed = Response from server doesn't indicate success, skipping upload
DeserializeResponseFailed = Failed to deserialize server response!
DownloadROMNotFound = Failed to find ROM that matches save, skipping download
DownloadMappingNotFound = Failed to find mapping, skipping download
DownloadServerDataFailed = Failed to get save data from server, skipping download
DownloadHashFailed = Downloaded file doesn't match server hash, skipping download
DownloadUnpackFailed = Failed to unpack save data, skipping download
SaveAlreadyTrackedDownload = A save is already being tracked, skipping download
DownloadExtractionPathFailed = Failed to set extraction path, skipping download
ExistingSaveTitle = Existing Save!
ExistingSaveConfirm = A save is already being tracked for this game, Do you want to replace the save being tracked?\n\nSlot:{$Slot}\nFilename:{$Filename}
SaveLocationTitle = Save location!
SaveLocationConfirm = Do you want the save to be unpacked here:\n{$Path}
ReplaceSaveTitle = Replace save
ReplaceSaveConfirm = {$GameName} already has a save being tracked do you want to replace it?

# Save
NoAutoDetectExtensions = One or more mappings have no auto detect extensions, they have been skipped!
PackSaveFailed = Failed to pack save archive to {$Path}
PackSaveFilesSkipped = One or more files/folders were skipped when packing save, see logs
ComputeHashArchiveEmpty = Failed to compute hash for {$Path} as the archive is empty, skipping
ComputeHashFailed = Failed to compute hash for {$Path}, skipping
ServerResponded = Server responded: {$Message}
ScreenCaptureNotSetup = Cannot start screenshot capture as setup wasn't completed!
SyncAlreadyRunning = A game is already running, cannot start sync!
SyncBeforeGameStartDisabled = 'Sync before game start' disabled, skipping sync!
SyncAfterGameQuitDisabled = 'Sync after game quit' disabled, skipping sync!
GameNotFoundSkipSync = Game with {$GameId} not found, skipping sync!
ServerNoResponseSync = The server failed to respond, skipping sync
NoSyncNeeded = No sync needed for {$GameName}
SyncStillConflicted = No option selected to resolve conflict for {$GameName} 
SaveFileZeroBytes = The save file for {$GameName} has 0 bytes, skipping sync
SaveFileMissingFiles = The save file for {$GameName} has missing files, skipping sync
SaveUploadSuccess = {$GameName} save backed up ({$Size})
SaveDownloadSuccess = {$GameName} save downloaded ({$Size})
QRLoginNotSupported = Your server doesn't support QR login (5.0.0+), Use client token or username/password instead!
ServerVersionLabel = Server Ver.
OpenInBrowser = Open in browser
Or = or
GameIsRunningWarning = Cannot change settings while a game is running
DevSettingsTab = Dev Settings

# Plugin Main
InstallFailed = Failed to install - {$Error}
OpenRomMLibrary = Open RomM library
OpenRomMProfile = Open RomM profile

# Authentication Page
AuthButton = Connect
ServerText = RomM Server Address
ClientToken = Client Token
ClientTokenLogin = Login with Client Token
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
AdvanceSigninHeader = Advanced: sign in with username and password
LoginQRTitle = Login with QR code
PhoneScanSubtitle = Scan with phone or open in browser.

# Mappings page
MappingsTitle = Mappings
MappingsSubTitle = Setup emulators for game importing
SyncPlatforms = Sync Platforms
PlatformsSynced = Synced {$PlatformCount} platforms
NewMapping = New Mapping
Configuration = Configuration
Emulator = Emulator
NoEmulator = No Emulator Selected
Profile = Profile
NoProfile = No Profile Selected
Platform = Platform
NoPlatform = No Platform Selected
ROMLoc = ROM Location
AutoExtractROMs = Automatically Extract Archived ROMs
PreferM3U = Prefer .m3u Files When Launching
AutoExtractROMsTooltip = Will extract ROMs if they are stored in RAR, Zip, Tar, 7Zip, GZip, Arc, Arj, Ace or Lzw format!
PreferM3UTooltip = Will use .m3u file if multi-file ROM and emulator supports .m3u!
SaveOptions = Save Options
AutoSaveDetection = Auto Detection Style
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
AutoSaveStylePlaceholder = No Auto-Save Style Selected
SetupIncomplete = Setup Incomplete
CustomEmulatorSet = Custom Emulator, no profile to select
NoEmulatorsSetup = No Emulators setup in Emunight

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
ScreenshotTitle = Screenshots
EnableScreenshots = Enable in-game screenshot capture for saves
MaxResolutionTitle = Max screenshot resolution
SecondsBeforeTitle = Screenshot timing
SecondsBeforeSubTitle = Grabs a screenshot X seconds before you save, so the shot doesn't catch a save-in-progress overlay

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
GetUserDataFailed = Failed to get user data from server

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
SaveStatusLocalNewer = Needs Upload
SaveStatusRemoteNewer = Needs Download
SaveStatusConflict = Conflicted
SaveStatusSynced = Synced
SaveStatusServerOnly = New on Server
SaveStatusUntrackedLocal = New on Disk
SaveStatusTempRestored = Temp Restored
SaveStatusMissingFiles = Missing Files
StatConflicts = Conflicts

# Archive Save Selector
ArchivedSavesTitle = Archived saves
NoSaves = No saves
SaveIsNull = Save is null, skipping
TrackArchivedSave = Track Archived Save
TrackArchivedSaveConfirm = Do you want to duplicate this save and start tracking it?

# Create Save Selector
CreateNewSave = Create New Save
MappingLabel = Mapping
NoMappingPlaceholder = No Mapping Selected
NoROMSelected = No ROM Selected
NoFilesFound = No Files Found
CreateSaveButton = Create Save
OverwriteSaveTitle = Overwrite Save?
OverwriteSaveConfirm = {$GameName} already has a tracked save, do you want to overwrite it?
CreateSaveConfirm = Creating a new save for {$GameName}?\nPaths:\n{$Paths}

# Resolve Conflict
SaveConflictDescription = Your local save conflicts with the save stored on RomM.
KeepServerSave = Keep Remote Save
KeepLocalSave = Keep Local Save

# Mapping Saves
EnableAllSaves = Enable All Saves
SaveManagerTitle = Save Management
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