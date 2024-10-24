# Visual Studio Workspace Manager

A Visual Studio extension that allows you to save and restore your window layouts, open documents, and cursor positions. Never lose your development context again!

## Features

- 📑 Save all currently open documents as a named workspace
- 🔄 Restore previously saved workspaces with a single click
- 📍 Preserves cursor positions in each file
- 🎯 Maintains tab groups and window arrangements
- 📋 Copy contents of all open documents to clipboard
- 💾 Stores workspaces per solution
- ⚡ Quick and easy to use through the Tools menu

## Installation

### Via Visual Studio Marketplace
1. Open Visual Studio
2. Go to Extensions > Manage Extensions
3. Search for "Visual Studio Workspace Manager"
4. Click Download and install

### Manual Installation
1. Download the `.vsix` file from the [releases page](../../releases)
2. Close all Visual Studio instances
3. Double-click the `.vsix` file
4. Follow the installation prompts

## Usage

### Saving a Workspace
1. Open the files you want to include in your workspace
2. Arrange your windows and tabs as desired
3. Go to Tools > Save Documents Workspace
4. Enter a name for your workspace
5. Click OK

### Loading a Workspace
1. Go to Tools > Load Documents Workspace
2. Select the workspace you want to restore
3. Confirm the operation
4. Your saved layout will be restored

### Copying All Documents
1. Go to Tools > Copy All Documents Contents
2. All open document contents are copied to your clipboard

## Configuration

The extension creates a `.vsworkspaces` folder in your solution directory to store workspace configurations. Each workspace is saved as a separate JSON file.

## Building from Source

Prerequisites:
- Visual Studio 2022
- Visual Studio SDK
- .NET Framework 4.7.2 or higher

Steps:
1. Clone the repository
```bash
git clone https://github.com/yourusername/vs-workspace-manager.git
```

2. Open the solution in Visual Studio
3. Build the solution

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

Please make sure to update tests as appropriate.

### Development Notes
- The extension is built using the Visual Studio SDK
- Uses EnvDTE for Visual Studio integration
- Written in C#

## Known Issues

- Some window positions might not be perfectly restored in multi-monitor setups
- Window states might not be perfectly preserved in certain Visual Studio layouts

## License

[MIT](LICENSE)

## Support

If you encounter any issues or have feature requests, please:
1. Check the [known issues](#known-issues)
2. Search existing [issues](../../issues)
3. Create a new issue if needed

## Version History

### 1.0.0
- Initial release
- Basic workspace save/load functionality
- Document content copying
- Tab group preservation

---

Made with ❤️ by Nadav Hury