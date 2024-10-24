using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Workspaces
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid("11111111-2222-3333-4444-555555555555")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class DocumentStatePackage : AsyncPackage
    {
        private DTE2 _dte;
        private const string WorkspacesFolderName = ".vsworkspaces";
        private const string SettingsFileName = "workspace_settings.json";
        private Settings _settings;

        protected override async Task InitializeAsync(System.Threading.CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var commandService = await GetServiceAsync((typeof(IMenuCommandService))) as IMenuCommandService;
            if (commandService != null)
            {
                var cmdSet = new Guid("11111111-2222-3333-4444-555555555555");
                var menuCommandID = new CommandID(cmdSet, 0x0100);
                var menuItem = new MenuCommand(SaveWorkspace, menuCommandID);
                commandService.AddCommand(menuItem);

                menuCommandID = new CommandID(cmdSet, 0x0101);
                menuItem = new MenuCommand(LoadWorkspace, menuCommandID);
                commandService.AddCommand(menuItem);

                menuCommandID = new CommandID(cmdSet, 0x0102);
                menuItem = new MenuCommand(CopyContents, menuCommandID);
                commandService.AddCommand(menuItem);
            }

            _dte = await GetServiceAsync(typeof(DTE)) as DTE2;
            if (_dte != null)
            {
                LoadSettings();
            }
        }

        private void LoadSettings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var solutionDir = Path.GetDirectoryName(_dte.Solution.FullName);
            var settingsPath = Path.Combine(solutionDir, WorkspacesFolderName, SettingsFileName);

            if (File.Exists(settingsPath))
            {
                _settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath));
            }
            else
            {
                _settings = new Settings { ShowLoadPrompt = true };
                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var solutionDir = Path.GetDirectoryName(_dte.Solution.FullName);
            var workspacesDir = Path.Combine(solutionDir, WorkspacesFolderName);
            Directory.CreateDirectory(workspacesDir);

            var settingsPath = Path.Combine(workspacesDir, SettingsFileName);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(_settings));
        }

        private string GetRelativePath(string fullPath, string basePath)
        {
            Uri fromUri = new Uri(basePath.EndsWith("\\") ? basePath : basePath + "\\");
            Uri toUri = new Uri(fullPath);

            string relativePath = Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString());
            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        private void SaveWorkspace(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Input dialog code remains the same...
            var inputDialog = new Form()
            {
                Width = 300,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Save Workspace",
                StartPosition = FormStartPosition.CenterScreen
            };
            var textBox = new TextBox() { Left = 50, Top = 20, Width = 200, Text = "NewWorkspace" };
            var buttonOk = new Button() { Text = "Ok", Left = 50, Width = 100, Top = 70, DialogResult = DialogResult.OK };
            var buttonCancel = new Button() { Text = "Cancel", Left = 150, Width = 100, Top = 70, DialogResult = DialogResult.Cancel };
            inputDialog.Controls.AddRange(new Control[] { textBox, buttonOk, buttonCancel });
            inputDialog.AcceptButton = buttonOk;
            inputDialog.CancelButton = buttonCancel;

            if (inputDialog.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(textBox.Text))
                return;

            var workspaceName = textBox.Text;
            var solutionDir = Path.GetDirectoryName(_dte.Solution.FullName);
            var workspacesDir = Path.Combine(solutionDir, WorkspacesFolderName);
            Directory.CreateDirectory(workspacesDir);

            var workspace = new Workspace { Name = workspaceName, Documents = new List<DocumentState>() };
            var tabGroups = new Dictionary<Window, int>();
            var currentGroup = 0;

            // First pass: identify tab groups
            foreach (Window window in _dte.Windows)
            {
                try
                {
                    if (window.Document != null && !tabGroups.ContainsKey(window))
                    {
                        tabGroups[window] = currentGroup++;
                    }
                }
                catch { }
            }

            // Save documents with their window states
            EnvDTE.Documents docs = _dte.Documents;
            foreach (EnvDTE.Document doc in docs)
            {
                try
                {
                    if (doc == null || string.IsNullOrEmpty(doc.FullName)) continue;

                    var window = doc.ActiveWindow;
                    var state = new DocumentState
                    {
                        FilePath = GetRelativePath(doc.FullName, solutionDir),
                        IsActive = doc.ActiveWindow != null && doc.ActiveWindow == _dte.ActiveWindow, // Fixed activation check
                        DocWindowTab = workspace.Documents.Count,
                    };

                    // Get cursor position
                    var textDoc = doc.Object("TextDocument") as TextDocument;
                    if (textDoc?.Selection != null)
                    {
                        var selection = textDoc.Selection;
                        state.Line = selection.CurrentLine;
                        state.Column = selection.CurrentColumn;
                    }

                    // Get window information
                    if (window != null)
                    {
                        state.WindowState = window.WindowState;
                        state.Left = window.Left;
                        state.Top = window.Top;
                        state.Width = window.Width;
                        state.Height = window.Height;
                        state.WindowKind = window.Kind;
                        state.IsFloating = window.IsFloating;
                        state.TabGroup = tabGroups.ContainsKey(window) ? tabGroups[window] : -1;
                    }

                    workspace.Documents.Add(state);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving document state: {ex.Message}");
                }
            }

            var workspacePath = Path.Combine(workspacesDir, $"{workspaceName}.json");
            File.WriteAllText(workspacePath, JsonSerializer.Serialize(workspace));

            VsShellUtilities.ShowMessageBox(
                this,
                $"Workspace '{workspaceName}' saved successfully",
                "Success",
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private void LoadWorkspace(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Workspace selection code remains the same...
            var solutionDir = Path.GetDirectoryName(_dte.Solution.FullName);
            var workspacesDir = Path.Combine(solutionDir, WorkspacesFolderName);

            if (!Directory.Exists(workspacesDir))
            {
                VsShellUtilities.ShowMessageBox(
                    this,
                    "No workspaces found",
                    "Error",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            var workspaces = Directory.GetFiles(workspacesDir, "*.json")
                .Where(f => !f.EndsWith(SettingsFileName))
                .Select(f => JsonSerializer.Deserialize<Workspace>(File.ReadAllText(f)))
                .ToList();

            var dialog = new WorkspaceSelector(workspaces);
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var selectedWorkspace = dialog.SelectedWorkspace;

            if (_settings.ShowLoadPrompt)
            {
                var promptDialog = new LoadPromptDialog();
                if (promptDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                _settings.ShowLoadPrompt = !promptDialog.DoNotShowAgain;
                SaveSettings();
            }

            // Close existing documents
            foreach (EnvDTE.Document doc in _dte.Documents)
            {
                try
                {
                    doc.Close(vsSaveChanges.vsSaveChangesNo);
                }
                catch { }
            }

            // Group documents by tab group
            var documentGroups = selectedWorkspace.Documents
                .OrderBy(d => d.DocWindowTab)
                .GroupBy(d => d.TabGroup)
                .ToList();

            Window lastActiveWindow = null;
            var firstWindows = new Dictionary<int, Window>();

            // Close existing documents
            foreach (EnvDTE.Document doc in _dte.Documents)
            {
                try
                {
                    doc.Close(vsSaveChanges.vsSaveChangesNo);
                }
                catch { }
            }

            // Open documents group by group
            foreach (var group in documentGroups)
            {
                Window firstWindowInGroup = null;

                foreach (var state in group)
                {
                    try
                    {
                        var fullPath = Path.GetFullPath(Path.Combine(solutionDir, state.FilePath));
                        if (!File.Exists(fullPath)) continue;

                        Window window;
                        if (firstWindowInGroup == null)
                        {
                            // First window in group opens normally
                            window = _dte.ItemOperations.OpenFile(fullPath);
                            firstWindowInGroup = window;
                            firstWindows[group.Key] = window;
                        }
                        else
                        {
                            // For subsequent windows, try to create them in a new horizontal tab group
                            window = _dte.ItemOperations.OpenFile(fullPath);
                            try
                            {
                                // Try to use VSCommands to arrange windows
                                if (firstWindowInGroup != null)
                                {
                                    window.Visible = true;
                                    firstWindowInGroup.Visible = true;
                                    var commands = _dte.Commands;
                                    var command = commands.Item("Window.NewHorizontalTabGroup");
                                    if (command != null && command.IsAvailable)
                                    {
                                        _dte.ExecuteCommand("Window.NewHorizontalTabGroup");
                                    }
                                }
                            }
                            catch { }
                        }

                        if (window?.Document == null) continue;

                        // Set cursor position
                        var textDoc = window.Document.Object("TextDocument") as TextDocument;
                        if (textDoc?.Selection != null)
                        {
                            var editPoint = textDoc.StartPoint.CreateEditPoint();
                            editPoint.LineDown(state.Line - 1);
                            editPoint.CharRight(state.Column);
                            textDoc.Selection.MoveToPoint(editPoint);
                        }

                        // Set window properties
                        try
                        {
                            window.WindowState = state.WindowState;
                            if (!state.IsFloating && window.WindowState != vsWindowState.vsWindowStateMaximize)
                            {
                                window.Left = state.Left;
                                window.Top = state.Top;
                                window.Width = state.Width;
                                window.Height = state.Height;
                            }
                        }
                        catch { }

                        if (state.IsActive)
                        {
                            lastActiveWindow = window;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading document: {ex.Message}");
                    }
                }
            }

            // Activate the last active window
            if (lastActiveWindow != null)
            {
                try
                {
                    lastActiveWindow.SetFocus();
                }
                catch { }
            }
        }

        private void CopyContents(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var contents = new System.Text.StringBuilder();
            foreach (Window window in _dte.Windows)
            {
                if (window.Document == null) continue;

                var textDoc = window.Document.Object("TextDocument") as TextDocument;
                if (textDoc == null) continue;

                contents.AppendLine($"// File: {window.Document.FullName}");
                contents.AppendLine(textDoc.CreateEditPoint().GetText(textDoc.EndPoint));
                contents.AppendLine();
            }

            if (contents.Length > 0)
            {
                Clipboard.SetText(contents.ToString());
                VsShellUtilities.ShowMessageBox(
                    this,
                    "Contents copied to clipboard",
                    "Success",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }

    public class DocumentState
    {
        public string FilePath { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public bool IsActive { get; set; }
        public int DocWindowTab { get; set; }  // Tab order

        // Window position and layout
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public vsWindowState WindowState { get; set; }
        public string WindowKind { get; set; }  // Store window kind for tab grouping
        public bool IsFloating { get; set; }    // Is window floating or docked
        public int TabGroup { get; set; }       // Which tab group this belongs to
    }

    public class ToolWindowState
    {
        public string Kind { get; set; }
        public string Caption { get; set; }
        public bool IsVisible { get; set; }
    }

    // Modify the Workspace class to include tool windows
    public class Workspace
    {
        public string Name { get; set; }
        public List<DocumentState> Documents { get; set; }
        public List<ToolWindowState> ToolWindows { get; set; }
    }

    public class Settings
    {
        public bool ShowLoadPrompt { get; set; }
    }

    
}