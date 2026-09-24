# Generic
Authentication = Authentication
Options = Options
Mappings = Emulator Mappings
Saves = Saves
Browse = Browse
Back = Back
Installation = Installation
Refresh = Refresh
Cancel = Cancel
Remote = Remote
Local = Local
LastModified = Last Modified
Game = Game
Name = Name
Value = Value
File = File
Enabled = Enabled
Dismiss = Dismiss
Start = Start
NoFolderPlaceholder = No Folder Selected
Disabled = Disabled
None = None
Download = Download
Skip = Skip
FilePaths = File Paths
FolderLabel = Folder
RemoveTooltip = Remove
Unknown = Unknown

# SettingsTabControl
GameIsRunningWarning = Cannot change settings while a game is running

# AuthenticationTab
ServerText = RomM server address
LoginWithClientToken = Login with Client Token
Connect = Connect
UseBasicAuth = Use Basic Auth
UserPassWarning = Username/password login is not recommended.
Username = Username
Password = Password
CustomHeaders = Custom Headers
CustomHeaderMalformed = Custom headers require both a name and value.
AdvancedOptions = Advanced Options
NewHeader = New Header
Login = Login
EnableBasicAuth = Enable username/password login before signing in.
LoginNoToken = Enter a client token before signing in.
AdvancedSigninHeader = Advanced: sign in with username and password
LoginQRTitle = Login with QR code
PhoneScanSubtitle = Scan the QR code or open the login page in your browser.
QRLoginNotSupported = Your server doesn't support QR login (5.0.0+). Use a client token or username/password instead.
ServerVersionLabel = Server Ver.
OpenInBrowser = Open in browser
ClientTokenAddressFailed = Cannot open the client token page because the RomM server address is not set.
ImageFiles = Image files

# MappingsTab
MappingsTitle = Mappings
MappingsSubTitle = Configure where RomM games are installed and launched
SyncPlatforms = Sync Platforms
PlatformsSynced = Synced {$PlatformCount} platforms
NewMapping = New Mapping
Configuration = Configuration
Emulator = Emulator
NoEmulator = Select an emulator
Profile = Profile
NoProfile = Select a profile
Platform = Platform
NoPlatform = Select a platform
ROMLoc = ROM Location
AutoExtractROMs = Automatically extract archived ROMs
PreferM3U = Prefer .m3u files when launching
AutoExtractROMsTooltip = Extracts ROMs stored in RAR, ZIP, TAR, 7z, GZip, ARC, ARJ, ACE, or LZW archives.
PreferM3UTooltip = Uses an .m3u file for multi-file ROMs when the emulator supports it.
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
CustomEmulatorSelected = Custom emulator selected; no profile is required.
NoEmulatorsSetup = No emulators are configured in EmuNight.
Updates = Updates
DLC = DLC
InstallMethod = Install method
InstallMode = Installation mode
InstallPath = Install path
CLIInstaller = CLI installer
InstallStyleTooltip =
    - External Folder: Copies downloaded content to the selected folder
    - Title ID Folder: Copies content to a folder based on the game's title ID
    - CLI: Uses the emulator's command line to install content
InstallModeTooltip =
    - Select One: Installs one selected file
    - Select Many: Lets you choose which files to install
    - Sequential: Installs multiple files in the order you choose
    - All: Installs all available files
CLIDefinitionTooltip = Select the command Graviton will use to install this content through the emulator.
InstallPathPlaceholder = Select an install folder

# Enum display values
InstallStyle_None = None
InstallStyle_ExternalFolder = External folder
InstallStyle_TitleIDFolder = Title ID folder
InstallStyle_CLI = CLI
InstallMode_SelectOne = Select one
InstallMode_SelectMany = Select multiple
InstallMode_Sequential = Sequential
InstallMode_All = All
SaveLayoutStyle_SingleFile = Single file
SaveLayoutStyle_FixedSet = Fixed set
SaveLayoutStyle_WholeFolder = Folder
SaveLayoutStyle_MemoryCard = Memory card
SaveLayoutStyle_Disabled = Disabled
CLIInstallRPCS3PKG = RPCS3 - Install PKG
CLIInstallVita3KPKG = Vita3K - Install PKG
CLIInstallVita3KArchive = Vita3K - Install Archive
CLIArgumentZRIF = ZRIF License Key

# OptionsTab
LibraryScanning = Library Scanning
MergeROMs = Merge ROM revisions
KeepDeleted = Keep games removed from RomM
SkipDeleted = Skip ROMs missing from RomM storage
ExcludeGenres = Exclude Genres
Use7z = Use 7z for archive extraction
NoExePlaceholder = Executable not set
SevenZipExecutable = 7-Zip executable
StatusSync = Status Sync
KeepStatusSynced = Keep completion status in sync with RomM
KeepFavouritesSynced = Keep favorites in sync with RomM
KeepPrivateNotesSynced = Keep private notes in sync with RomM
KeepPublicNotesSynced = Keep public notes in sync with RomM
SaveSync = Save Sync
DownloadSaveOnLaunch = Download save on game launch
UploadSaveOnFinished = Upload save after game finished
SaveConflictsLabel = Save conflicts
AutoCleanOldSaves = Auto-clean old saves on server, keeping the newest
SaveStateSync = Save State Sync
ExcludeGenresPlaceholder = Adventure;Platform;RPG;
ScreenshotTitle = Screenshots
EnableScreenshots = Enable in-game screenshot capture for saves
MaxResolutionTitle = Max screenshot resolution
SecondsBeforeTitle = Screenshot timing
SecondsBeforeSubTitle = Grabs a screenshot X seconds before you save, so the shot doesn't catch a save-in-progress overlay
AddCollection = Create Playnite category from manual collection
AddVirtualCollection = Create Playnite category from virtual collection
AddSmartCollection = Create Playnite category with smart collection
ImportPlaySessions = Import play sessions
DebuggingTitle = Debugging
EnableDebugging = Enable debugging
SaveConflictResolve_Ask = Ask
SaveConflictResolve_PreferRemote = Prefer remote
SaveConflictResolve_PreferLocal = Prefer local
ImportPlaySessions_None = None
ImportPlaySessions_OnlyThisDevice = Only this device
ImportPlaySessions_All = All
ScreenshotResolution_P720 = 720p
ScreenshotResolution_P1080 = 1080p
ScreenshotResolution_P1440 = 1440p
ScreenshotResolution_UHD4K = 4K

# Save Management
SaveManagerTitle = Save Management
FilterByGameName = Filter by game name
CreateNewSave = Create new save
TrackArchivedSave = Track archived save
ArchivedSavesTitle = Archived saves
NoSaves = No saves
SlotLabel = Slot
SyncTooltip = Sync
SyncUploadTooltip = Sync (Upload)
SyncDownloadTooltip = Sync (Download)
ResolveConflictTooltip = Resolve conflict
StartTrackingDownloadTooltip = Start tracking (Download)
StartTrackingUploadTooltip = Start tracking (Upload)
UploadRestoredSaveTooltip = Upload restored save
ResolveMissingFilesTooltip = Resolve missing files
AddFileTooltip = Add file
AddFolderTooltip = Add folder
History = History
Current = Current
SwitchToThis = Switch to this
NoSaveHistory = No save history
ManualSyncLabel = Manual sync:
ForceUpload = Force upload
ForceUploadTooltip = Uploads the local save, overwriting the copy on the server
ForceDownload = Force download
ForceDownloadTooltip = Downloads the server save, overwriting the local copy
DuplicateToArchive = Duplicate to archive
DuplicateToArchiveTooltip = Makes a permanent copy of the save that future syncs and auto-cleanup will never overwrite or delete
RestoreLocallyOnly = Restore locally only
RestoreAndSync = Restore & sync
UntrackSaveButton = Untrack save
NoROMsForMapping = No games were found for this mapping, so a new save cannot be created.
SaveAlreadySynced = Save is already in sync.
UploadRestoredSaveConfirm = How do you want to upload the restored save?
UploadRestoredSaveTitle = Upload restored save
MissingFilesConfirm = Some files or folders were missing during the last sync. Stop tracking these paths?{$Paths}
RestoreHistoricSaveTitle = Restore historic save
SaveStatusUnknownWarning = Save status is unknown, skipping.
ROMNotFoundForSave = The ROM for this save could not be found.
ParentSaveNotFound = The parent save could not be found.
RestoreHistoricSaveConfirm = How do you want to restore the historic save?
GameNotFoundForSave = The game associated with this save could not be found.
MappingNotFoundForGame = The mapping for this game could not be found.
FilesOutsideMappingDir = Some files or folders were outside the configured save directory and were skipped.
TrackArchivedSaveConfirm = Duplicate this save and start tracking the copy?
MappingLabel = Mapping
NoMappingPlaceholder = Select a mapping
NoROMSelected = Select a game
NoFilesFound = No save files found
CreateSaveButton = Create save
OverwriteSaveTitle = Overwrite save?
OverwriteSaveConfirm = {$GameName} already has a tracked save. Replace it?
CreateSaveConfirm = Create a new save for {$GameName}?\n\nFiles and folders:\n{$Paths}
SaveConflict = Save conflict
SaveConflictDescription = The local save differs from the save stored on RomM.
KeepRemoteSave = Keep remote save
KeepLocalSave = Keep local save
DeleteSaveLocal = Delete local copy
DeleteSaveBoth = Delete everywhere
SaveNoGameSelected = No game was selected, so a save backup cannot be created.
DeleteSaveTitle = Delete save?
DeleteSaveMessage = How do you want to delete the save?
DeleteMappingTitle = Delete mapping
DeleteMappingConfirmation = Are you sure you want to delete this mapping?
ExistingSaveTitle = Existing save
ExistingSaveConfirm = A save is already being tracked for this game. Replace it?\n\nSlot: {$Slot}\nFilename: {$Filename}
SaveLocationTitle = Save location
SaveLocationConfirm = Extract the save to:\n{$Path}
ReplaceSaveTitle = Replace save
ReplaceSaveConfirm = {$GameName} already has a tracked save. Replace it?
SaveStatusUnknown = Unknown
UnknownGame = -- UNKNOWN GAME --
SaveStatusLocalNewer = Needs upload
SaveStatusRemoteNewer = Needs download
SaveStatusConflict = Conflicted
SaveStatusSynced = Synced
SaveStatusServerOnly = New on server
SaveStatusUntrackedLocal = New on disk
SaveStatusTempRestored = Temp restored
SaveStatusMissingFiles = Missing files
StatConflicts = Conflicts
LastSyncedNever = Never synced
FoundOnDisk = Found on disk
TimeSecondsAgo = {$Count}s ago
TimeMinutesAgo = {$Count}m ago
TimeHoursAgo = {$Count}h ago
TimeYesterday = Yesterday, {$Time}
TimeDaysAgo = {$Count}d ago

# Save Notifications
SyncCannotStart = Cannot perform save sync operations while a game is running.
UploadROMNotFound = Could not find the ROM for this save.
UploadMappingNotFound = Could not find the mapping for this save.
UploadPackFailed = Could not create the save archive.
UploadPathsSkipped = Some paths were skipped while creating the save archive.
UploadFilesMissing = Save files are missing.
UploadConflictResolveFailed = Could not resolve the save conflict.
UploadServerFailed = The server did not report a successful upload.
DeserializeResponseFailed = Could not read the server response.
DownloadROMNotFound = Could not find the ROM for this save.
DownloadMappingNotFound = Could not find the mapping for this save.
DownloadServerDataFailed = Could not get the save data from the server.
DownloadHashFailed = Downloaded save failed integrity verification.
DownloadUnpackFailed = Could not extract the downloaded save.
SaveAlreadyTrackedDownload = A save is already being tracked for this game.
DownloadExtractionPathFailed = Could not set the save extraction path.
NoAutoDetectExtensions = One or more mappings have no auto-detect extensions and were skipped.
PackSaveFailed = Could not create the save archive at {$Path}.
PackSaveFilesSkipped = Some files or folders were skipped while creating the save archive. See the logs for details.
ComputeHashArchiveEmpty = Could not compute a hash for {$Path} because the archive is empty.
ComputeHashFailed = Could not compute a hash for {$Path}.
ScreenCaptureNotSetup = Screenshot capture could not start because setup is incomplete.
SyncAlreadyRunning = A game is already running, so sync cannot start.
SyncBeforeGameStartDisabled = Sync before game start is disabled.
SyncAfterGameQuitDisabled = Sync after game quit is disabled.
GameNotFoundSkipSync = Game {$GameId} could not be found, skipping sync.
ServerNoResponseSync = The server did not respond, skipping sync.
NoSyncNeeded = No sync needed for {$GameName}
SyncStillConflicted = No option was selected to resolve the conflict for {$GameName}.
SaveFileZeroBytes = The save file for {$GameName} is empty, skipping sync.
SaveFileMissingFiles = The save for {$GameName} has missing files, skipping sync.
SaveUploadSuccess = {$GameName} save backed up ({$Size})
SaveDownloadSuccess = {$GameName} save downloaded ({$Size})
FailedDeserialize = Could not read save data: {$Error}
FailedNegotiateSaves = Failed to negotiate save: {$Error}
SaveArchiveNotFound = Save archive not found at {$SaveLoc}.
ArchiveResolvesOutside = Archive entry '{$Entry}' resolves outside the destination.
ExtractionEmpty = Extraction reported success, but the archive is empty.
FailedUnpack = Could not extract the save archive at {$SaveLoc}.
SaveIsNull = Save is null, skipping.

# Plugin Main
InstallFailed = Installation failed: {$Error}
OpenRomMLibrary = Open RomM library
OpenRomMProfile = Open RomM profile

# Http Client
ServerResponded = Server responded: {$Message}
Reauthenticate = Reauthentication required.
GETFailed = GET request failed for {$APIPath}
POSTFailed = POST request failed for {$APIPath}
PUTFailed = PUT request failed for {$APIPath}
DELETEFailed = DELETE request failed for {$APIPath}
HEADFailed = HEAD request failed for {$APIPath}

# Import
ROMFileMissing = A file for ROM ID {$ROMID} could not be found on the RomM server.
ROMImportFailed = Could not import {$GameName} [ID: {$ROMID}], skipping.
SaveROMDataFailed = Could not save game data: {$Error}
GameUpdateFailed = One or more games could not be updated.
ROMImportMultipleFailed = One or more games could not be imported.

# Import controller
NoEmulatorsConfigured = No emulators are configured or enabled in RomM settings. No games will be imported.
IncompleteMappingsSkipped = One or more incomplete mappings were skipped.
PlatformNotFound = Platform {$PlatformName} (ID: {$PlatformID}) was not found in RomM, skipping.
DownloadROMDataFailed = Failed to download ROMs for {$PlatformName}: {$Error}
ManualCollectionsFailed = Could not get manual collections: {$Error}
SmartCollectionsFailed = Could not get smart collections: {$Error}
ServerReturnedNullData = Server returned null data.
DeserializeFailed = Deserialization failed.
DeserializeCollectionFailed = Failed to deserialize collection.

# Account
HeartbeatFailed = Server heartbeat request failed.
HostNotConfigured = RomM server address is not configured. Set it in the settings.
HostInvalid = RomM server address is invalid. Check the URL in the settings.
InvalidScheme = The RomM server address must use HTTP or HTTPS.
UserPassNotSet = Cannot log in because the username or password is not set.
TokenNotSet = Cannot log in because the client token is not set.
LoginSuccessful = Login successful.
NotAuthenticated = You are not logged in. Please sign in.
ProfileIconFailed = Could not get the profile image: {$Error}
GetDevicesFailed = Could not get RomM devices: {$Error}
CreateDeviceFailed = Could not create a new device: {$Error}
FavouritesUpdateFailed = Can't update favorites, collection is null
NoPlatforms = No platforms retrieved from server!
QRCodeSetupFailed = Could not set up QR login: {$Error}
PairFailed = Could not pair with the RomM server: {$Error}
PairExpired = Expired
PairWasDenied = Request was denied
PairExpiresIn = Expires in: {$Seconds}s
AccountUserInfoDeserializeFailed = Could not read user information from the server.
AccountServerResponseFailed = The server did not report a successful response ({$Status}).
AccountAvatarTooLarge = The profile image exceeds the maximum allowed size.
AccountProfilePathMissing = The profile image could not be saved because the plugin data path is unavailable.
AccountNullResponse = The server returned an empty response.
AccountResponseDeserializeFailed = Could not read the server response.
AccountDeviceIdMissing = The server response did not include a device ID.
AccountAccessTokenMissing = The server response did not include an access token.
AccountUnexpectedStatus = The server returned an unexpected status: {$Status}
AccountDeviceResponseDeserializeFailed = Could not read the device registration response.

# Settings
SettingsSaveFailed = Could not save settings: {$Error}
SettingsLoadFailed = Could not load settings: {$Error}

# Download / Installation
DownloadViewName = RomM Downloads
DownloadViewTitle = Downloads
DownloadFailed = Failed to download {$GameName}: {$Error}
DownloadStatusQueued = Queued
DownloadStatusCompleted = Completed
DownloadStatusCanceled = Canceled
DownloadStatusFailed = Failed
DownloadStatusDownloading = Downloading…
DownloadStatusDownloadingPct = Downloading… {$Percent}%
DownloadStatusExtracting = Extracting…
DownloadStatusExtractingPct = Extracting… {$Percent}%
GameNotFoundTitle = Game not found
GameFolderNotFound = The folder for "{$GameName}" could not be found. The game will be marked as uninstalled.
UninstallFailed = Could not delete the game files. The game will be marked as uninstalled. {$Error}
InstallLibraryGameIdMissing = The game does not have a library game ID.
InstallMappingDataMissing = The emulator mapping data could not be found. Try removing and re-adding the mapping.
InstallGameDataMissing = The Playnite game entry could not be found.
InstallROMDataMissing = The RomM game data could not be found.
InstallGameIdNotFound = Game ID {$GameID} could not be found.
InstallMappingNotFound = The emulator mapping for this game could not be found.
DownloadServerNullResponse = The server returned an empty download response.
ArchiveExtractionFailed = Archive extraction failed for {$Path} with exit code {$ExitCode}.

# Play Controller
PlayMappingNotFound = Could not find the emulator mapping for this game.
PlayEmulatorNotSet = No emulator is configured for this game's mapping.
PlayMappingIncomplete = This game's mapping is not fully configured and the game cannot be launched.
InstalledFileUnsupported = The installed game file is not supported by the selected emulator or profile.
LaunchFileNotFound = Could not find a supported game file for the selected emulator or profile.
GameDataMissing = Game data could not be found. Please reinstall the game.
ProfileSettingsMissing = Could not find the selected emulator profile settings. The game cannot be launched.
StartupArgumentsMissing = Could not find startup arguments. Check the emulator settings in EmuNight.
EmulatorInstallDirectoryMissing = The emulator install directory is not configured. Check the emulator settings in EmuNight.
ExecutablePatternMissing = The selected profile does not define an executable pattern.
EmulatorExecutableNotFound = Could not find an emulator executable matching the selected profile.

# Game Session Handler
GameIDMissing = Game ID is missing. The game cannot be launched.
StartPropertiesMissing = One or more required launch properties are missing. The game cannot be launched.
LifecycleProfileSettingsMissing = Could not find settings for the selected emulator profile, skipping the script.
LifecycleEmulatorMissing = Could not find the emulator for this mapping, skipping the script.
EmulatorScriptFailed = Emulator script failed: {$Error}

# Status Controller
GameIDParseFailed = Could not read game ID {$GameID}, skipping.
CompletionStatusNameFailed = Could not get the completion status name.
CompletionStatusConversionFailed = {$PlayniteStatus} cannot be converted to a RomM completion status.
ActivityHeartbeatFailed = Could not send the activity heartbeat.
UserDataFailed = Could not get user data from RomM.
GetPlaySessionsFailed = Could not get play sessions.
