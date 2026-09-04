using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.Versioning;
using FolderHue.App.Icons;
using FolderHue.Core.Folders;
using FolderHue.Core.Palette;
using FolderHue.Core.Resources;
using FolderHue.Core.Storage;

namespace FolderHue.App;

/// <summary>
/// Fenetre de reglages : apercu de la palette, gestion des dossiers colorises, desinstallation propre.
/// </summary>
/// <remarks>
/// Aucun libelle en dur : tout passe par <see cref="Loc"/> (CLAUDE.md §7).
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class MainForm : Form
{
    private readonly FolderCustomizer _customizer = FolderCustomizer.CreateDefault();
    private readonly IconLibrary _library = IconLibrary.CreateDefault();

    private readonly ListView _paletteView = new();
    private readonly ComboBox _emblemPicker = new();
    private readonly ListView _foldersView = new();
    private readonly Label _status = new();
    private readonly Button _regenerate = new();

    private ImageList? _paletteImages;

    internal MainForm()
    {
        Text = Loc.Get("App_Title");
        MinimumSize = new Size(760, 520);
        Size = new Size(880, 600);
        StartPosition = FormStartPosition.CenterScreen;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildPaletteTab());
        tabs.TabPages.Add(BuildFoldersTab());
        tabs.TabPages.Add(BuildAboutTab());

        _status.Dock = DockStyle.Bottom;
        _status.Height = 28;
        _status.Padding = new Padding(8, 6, 8, 6);
        _status.TextAlign = ContentAlignment.MiddleLeft;

        Controls.Add(tabs);
        Controls.Add(_status);

        Load += OnLoaded;
    }

    private TabPage BuildPaletteTab()
    {
        var page = new TabPage(Loc.Get("App_PaletteTab")) { Padding = new Padding(12) };

        _paletteView.Dock = DockStyle.Fill;
        _paletteView.View = View.LargeIcon;
        _paletteView.MultiSelect = false;

        _emblemPicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _emblemPicker.Width = 220;
        _emblemPicker.SelectedIndexChanged += (_, _) => RefreshPalettePreview();

        foreach (Emblem emblem in PaletteCatalog.Emblems)
        {
            _emblemPicker.Items.Add(Loc.Get(emblem.ResourceKey));
        }

        _emblemPicker.SelectedIndex = 0;

        _regenerate.Text = Loc.Get("App_RegenerateIcons");
        _regenerate.AutoSize = true;
        _regenerate.Click += OnRegenerateClicked;

        var openIcons = new Button { Text = Loc.Get("App_OpenIconsFolder"), AutoSize = true };
        openIcons.Click += (_, _) => OpenInExplorer(AppPaths.Default.IconsDirectory);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8),
        };

        toolbar.Controls.Add(new Label
        {
            Text = Loc.Get("App_ColumnEmblem"),
            AutoSize = true,
            Padding = new Padding(0, 6, 6, 0),
        });
        toolbar.Controls.Add(_emblemPicker);
        toolbar.Controls.Add(_regenerate);
        toolbar.Controls.Add(openIcons);

        page.Controls.Add(_paletteView);
        page.Controls.Add(toolbar);
        return page;
    }

    private TabPage BuildFoldersTab()
    {
        var page = new TabPage(Loc.Get("App_FoldersTab")) { Padding = new Padding(12) };

        _foldersView.Dock = DockStyle.Fill;
        _foldersView.View = View.Details;
        _foldersView.FullRowSelect = true;
        _foldersView.Columns.Add(Loc.Get("App_ColumnFolder"), 400);
        _foldersView.Columns.Add(Loc.Get("App_ColumnColor"), 110);
        _foldersView.Columns.Add(Loc.Get("App_ColumnEmblem"), 110);
        _foldersView.Columns.Add(Loc.Get("App_ColumnDate"), 150);
        _foldersView.DoubleClick += (_, _) => OpenSelectedFolder();

        var resetSelected = new Button { Text = Loc.Get("App_ResetSelected"), AutoSize = true };
        resetSelected.Click += OnResetSelectedClicked;

        var resetAll = new Button { Text = Loc.Get("App_ResetAll"), AutoSize = true };
        resetAll.Click += OnResetAllClicked;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 8),
        };

        toolbar.Controls.Add(resetSelected);
        toolbar.Controls.Add(resetAll);

        page.Controls.Add(_foldersView);
        page.Controls.Add(toolbar);
        return page;
    }

    private static TabPage BuildAboutTab()
    {
        var page = new TabPage(Loc.Get("App_AboutTab")) { Padding = new Padding(16), AutoScroll = true };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
        };

        layout.Controls.Add(new Label
        {
            Text = Loc.Get("App_Name"),
            Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, 14f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
        });

        // Consequence assumee du stockage centralise des icones (CLAUDE.md §4.2).
        layout.Controls.Add(Paragraph(Loc.Get("App_PortabilityWarning")));

        // Comportement normal a documenter plutot qu'a corriger (CLAUDE.md §10).
        layout.Controls.Add(Paragraph(Loc.Get("App_VersionControlNote")));

        var openLogs = new LinkLabel
        {
            Text = AppPaths.Default.LogsDirectory,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 0),
        };
        openLogs.LinkClicked += (_, _) => OpenInExplorer(AppPaths.Default.LogsDirectory);
        layout.Controls.Add(openLogs);

        page.Controls.Add(layout);
        return page;
    }

    private static Label Paragraph(string text) => new()
    {
        Text = text,
        AutoSize = false,
        Width = 780,
        Height = 60,
        Margin = new Padding(0, 0, 0, 12),
    };

    private void OnLoaded(object? sender, EventArgs e)
    {
        RefreshFolders();

        if (_library.IsUpToDate())
        {
            RefreshPalettePreview();
            _status.Text = Loc.Format("App_IconsReady", PaletteCatalog.IconCombinationCount);
        }
        else
        {
            StartGeneration(force: false);
        }
    }

    private void OnRegenerateClicked(object? sender, EventArgs e) => StartGeneration(force: true);

    private void StartGeneration(bool force)
    {
        _regenerate.Enabled = false;
        _status.Text = Loc.Get("App_GeneratingIcons");

        var worker = new BackgroundWorker { WorkerReportsProgress = true };

        worker.DoWork += (_, args) =>
        {
            var progress = new Progress<(int Done, int Total)>(p => worker.ReportProgress(p.Done * 100 / p.Total));
            args.Result = _library.EnsureAll(force, progress);
        };

        worker.ProgressChanged += (_, args) =>
            _status.Text = $"{Loc.Get("App_GeneratingIcons")} {args.ProgressPercentage} %";

        worker.RunWorkerCompleted += (_, args) =>
        {
            _regenerate.Enabled = true;

            if (args.Error is not null)
            {
                Log.Default.Error("La generation des icones a echoue.", args.Error);
                _status.Text = args.Error.Message;
                return;
            }

            _status.Text = Loc.Format("App_IconsReady", PaletteCatalog.IconCombinationCount);
            RefreshPalettePreview();
        };

        worker.RunWorkerAsync();
    }

    private void RefreshPalettePreview()
    {
        IconRenderer? renderer = IconLibrary.TryCreateRenderer();
        if (renderer is null)
        {
            return;
        }

        Emblem emblem = PaletteCatalog.Emblems[Math.Max(0, _emblemPicker.SelectedIndex)];

        ImageList images = new() { ImageSize = new Size(48, 48), ColorDepth = ColorDepth.Depth32Bit };
        _paletteView.BeginUpdate();
        _paletteView.Items.Clear();

        foreach (FolderColor color in PaletteCatalog.Colors)
        {
            using Bitmap? preview = renderer.RenderPreview(color, emblem, 48);
            if (preview is null)
            {
                continue;
            }

            images.Images.Add(color.Id, (Image)preview.Clone());
            _paletteView.Items.Add(new ListViewItem(Loc.Get(color.ResourceKey), color.Id));
        }

        _paletteView.LargeImageList = images;
        _paletteView.EndUpdate();

        _paletteImages?.Dispose();
        _paletteImages = images;
    }

    private void RefreshFolders()
    {
        // Un dossier supprime hors de l'application y laisse sa trace : rien ne nous en informe.
        // La liste est le seul endroit ou l'utilisateur la verrait, donc celui ou on la retire.
        _customizer.Journal.PruneMissing();

        _foldersView.BeginUpdate();
        _foldersView.Items.Clear();

        foreach (AppliedEntry entry in _customizer.Journal.ReadAll().OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase))
        {
            FolderColor? color = PaletteCatalog.FindColor(entry.ColorId);
            Emblem? emblem = PaletteCatalog.FindEmblem(entry.EmblemId);

            var item = new ListViewItem(entry.Path) { Tag = entry.Path };
            item.SubItems.Add(color is null ? entry.ColorId : Loc.Get(color.ResourceKey));
            item.SubItems.Add(emblem is null ? entry.EmblemId : Loc.Get(emblem.ResourceKey));
            item.SubItems.Add(entry.AppliedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));

            _foldersView.Items.Add(item);
        }

        _foldersView.EndUpdate();
    }

    private void OnResetSelectedClicked(object? sender, EventArgs e)
    {
        if (_foldersView.SelectedItems.Count == 0)
        {
            return;
        }

        foreach (ListViewItem item in _foldersView.SelectedItems)
        {
            Reset((string)item.Tag!);
        }

        RefreshFolders();
    }

    private void OnResetAllClicked(object? sender, EventArgs e)
    {
        if (_foldersView.Items.Count == 0)
        {
            return;
        }

        DialogResult confirmation = MessageBox.Show(
            Loc.Get("App_ResetAllConfirm"),
            Loc.Get("App_Name"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        foreach (AppliedEntry entry in _customizer.Journal.ReadAll().ToArray())
        {
            Reset(entry.Path);
        }

        RefreshFolders();
    }

    private void Reset(string path)
    {
        OperationResult result = _customizer.Reset(path);

        if (!result.Success)
        {
            _status.Text = Loc.Get(result.ReasonKey ?? string.Empty);
        }
    }

    private void OpenSelectedFolder()
    {
        if (_foldersView.SelectedItems.Count == 1)
        {
            OpenInExplorer((string)_foldersView.SelectedItems[0].Tag!);
        }
    }

    private static void OpenInExplorer(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true })?.Dispose();
        }
        catch (Exception e) when (e is Win32Exception or IOException or UnauthorizedAccessException)
        {
            Log.Default.Warn($"Impossible d'ouvrir « {path} » : {e.Message}");
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _paletteImages?.Dispose();
        }

        base.Dispose(disposing);
    }
}
