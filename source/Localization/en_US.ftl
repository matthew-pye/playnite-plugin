# Generic
Authentication = Authentication
Options = Options
Mappings = Emulator Mappings
Saves = Saves
States = Save States
Browse = Browse
Installation = Installation
Refresh = Refresh

# Authentication Page
AuthButton = Authenticate
ServerText = RomM Server Address
ClientToken = Client Token
UseBasicAuth = Use Basic Auth
UserPassWarning = Warning! Using basic login NOT recommended!
Username = Username
Password = Password
InvaildScheme = Host address must start with http or https

# Mappings page
SyncPlatforms = Sync Platforms
PlatformsSynced = Synced {$PlaformCount} platforms
NewMapping = New Mapping
Emulator = Emulator
Profile = Profile
Platform = Platform
ROMLoc = ROM Location
AutoExtractROMs = Automatically Extract Archived ROMs
PreferM3U = Prefer .m3u Files When Launching

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

# Http Client
Reauthenticate = Reauthentication required!
GETFailed = GET Request Failed For {$APIPath}
POSTFailed = POST Request Failed For {$APIPath}
PUTFailed = PUT Request Failed For {$APIPath}
DELETEFailed = DELETE Request Failed For {$APIPath}

# Import
NoFileNameWithID = The filename for ROM ID {$ROMID} does not exist. Does the ROM exist on the server's file system?
ROMImportFailed = Failed to import 
ROMDataSaveFailed = Failed to save ROM data to disk

# Import controller
NoEmulatorsConfigured = No emulators are configured or enabled in RomM settings. No games will be imported.
PlatformNotFound = Platform {$PlatformName} (ID: {$PlatformID}) was not found in RomM. Skipping.
DownloadROMDataFailed = Failed to download ROMs for {$PlatformName}: {$Error}

# Account
NewProfileIconFailed = Failed to upload new profile image
ClientTokenAddressFailed = Cannot open the client token page because the RomM server address is not set.
HeartbeatFailed = Server heartbeat request failed.
HostNotSet = RomM host is not configured. Set it in the settings
HostInvaild = RomM host is invalid. Please check the URL in the settings.
UserPassNotSet = Cannot log in because the username or password is not set.
TokenNotSet = Cannot log in because the client token is not set.
LoginSuccess = Login Successful!
NotAuthenticated = User is not authenticated. Please Log in.
GETProfileIconFailed = Failed to get profile icon
GETDevicesFailed = Failed to get RomM devices
CreateNewDeviceFailed = Failed to create new device
FavouritesUpdateFailed = Can't update favorites, collection is null

# Settings
SettingSaveFailed = Failed to save settings
SettingLoadFailed = Failed to load settings

# Downloads
DownloadViewName = RomM Downloads
DownloadViewTitle = Downloads
DownloadFailed = Failed to download

# Status Controller
LibraryIdConvertFailed = Failed to parse GameID, Skipping status update!
CompletionStatusNameFailed = Failed to get name of completion status
ConvertStatusFailed = {$PlayniteStatus} cannot be converted to a RomM status
GameHeartbeatFailed = Failed to send activity heartbeat 

# Mapping Saves
EnableAllSaves = Enable All Saves
SaveManagerTitle = Save Manager
LocalSaves = Local Saves
RemoteSaves = Remote Saves
NoSavePathSet = Save Path in mapping not set cannot download save!