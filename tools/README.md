# Local Playnite Toolbox

For automatic Release packaging, place the Playnite 11 `Toolbox.exe` in this folder, alongside the `source` directory:

```text
playnite-11-plugin/
├── tools/
│   └── Toolbox.exe
└── source/
```

Alternatives:

- Set the `PLAYNITE_TOOLBOX` environment variable to the full path to `Toolbox.exe`.
- Pass `-p:PlayniteToolbox=C:\path\to\Toolbox.exe` to `dotnet build`.

Build behavior:

- `dotnet build -c Debug` builds normally and generates `extension.toml` in the Debug output.
- `dotnet build -c Release` generates `extension.toml`, removes all native runtime folders except `runtimes\win-x64` from the Release output, stages the package under `bin\Release\package`, and creates `bin\Release\Graviton-<version>.pext2`.
