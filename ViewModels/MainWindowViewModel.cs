// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using DTC.Core.Commands;
using DTC.Core.Extensions;
using DTC.Core.UI;
using Avalonia.Media.Imaging;
using Material.Icons;
using PromoG33k.Models;
using PromoG33k.Services;
using PromoG33k.Settings;

namespace PromoG33k.ViewModels;

/// <summary>
/// View model for the main promotion queue dashboard.
/// </summary>
/// <remarks>
/// The first screen is intentionally queue-first so the app helps decide what is worth promoting next.
/// </remarks>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ClipboardService m_clipboardService;
    private readonly PromotionScoreService m_promotionScoreService;
    private readonly LocalRepositoryScanner m_localRepositoryScanner;
    private readonly OpenAiPostGenerationService m_openAiPostGenerationService;
    private readonly AppSettings m_settings;
    private readonly HttpClient m_imageHttpClient = new HttpClient();
    private RepositoryProfile m_selectedRepository;
    private string m_draftText;
    private string m_statusText = "Ready.";
    private bool m_isBusy;
    private bool m_isSettingsOpen;
    private string m_settingsLocalRepositoryRootPath;
    private string m_settingsOpenAiApiKey;
    private string m_settingsOpenAiModel;
    private int m_sortModeIndex;
    private int m_selectedPostStyleIndex;

    public MainWindowViewModel()
        : this(
            new ClipboardService(),
            new PromotionScoreService(),
            new LocalRepositoryScanner(),
            new OpenAiPostGenerationService(),
            AppSettings.Instance)
    {
    }

    internal MainWindowViewModel(
        ClipboardService clipboardService,
        PromotionScoreService promotionScoreService,
        LocalRepositoryScanner localRepositoryScanner,
        OpenAiPostGenerationService openAiPostGenerationService,
        AppSettings settings)
    {
        m_clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        m_promotionScoreService = promotionScoreService ?? throw new ArgumentNullException(nameof(promotionScoreService));
        m_localRepositoryScanner = localRepositoryScanner ?? throw new ArgumentNullException(nameof(localRepositoryScanner));
        m_openAiPostGenerationService = openAiPostGenerationService ?? throw new ArgumentNullException(nameof(openAiPostGenerationService));
        m_settings = settings ?? throw new ArgumentNullException(nameof(settings));
        CopyDraftCommand = new AsyncRelayCommand(_ => CopyDraftAsync());
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        CloseSettingsCommand = new RelayCommand(_ => IsSettingsOpen = false);
        SaveSettingsCommand = new AsyncRelayCommand(_ => SaveSettingsAsync());
        BrowseLocalRepositoryRootCommand = new AsyncRelayCommand(_ => BrowseLocalRepositoryRootAsync());
        OpenOpenAiApiKeysCommand = new RelayCommand(_ => OpenUrl("https://platform.openai.com/api-keys"));
        RefreshRepositoriesCommand = new AsyncRelayCommand(_ => LoadRepositoriesAsync());
        TestOpenAiCommand = new AsyncRelayCommand(_ => TestOpenAiAsync());
        GenerateDraftCommand = new AsyncRelayCommand(_ => GenerateDraftAsync(), _ => SelectedRepository != null);
        GenerateDraftWithInstructionCommand = new AsyncRelayCommand(_ => GenerateDraftWithInstructionAsync(), _ => SelectedRepository != null);
        MarkSelectedDraftAsUsedCommand = new RelayCommand(_ => MarkSelectedDraftAsUsed(), _ => SelectedRepository != null);
        OpenSelectedRepositoryCommand = new RelayCommand(_ => OpenSelectedRepository(), _ => SelectedRepository != null);
        OpenUrlCommand = new RelayCommand(parameter => OpenUrl(parameter as string), parameter => parameter is string url && Uri.TryCreate(url, UriKind.Absolute, out _));
        LoadSettingsFields();

        Repositories = [];
        SelectedScreenshotPreviews = [];
        foreach (var repository in m_settings.Repositories ?? [])
            Repositories.Add(repository);
        SelectedRepository = Repositories.FirstOrDefault();
    }

    public ObservableCollection<RepositoryProfile> Repositories { get; }
    public ObservableCollection<ScreenshotPreviewViewModel> SelectedScreenshotPreviews { get; }

    public RepositoryProfile SelectedRepository
    {
        get => m_selectedRepository;
        set
        {
            PersistSelectedRepositoryDraft();
            if (!SetProperty(ref m_selectedRepository, value))
                return;
            DraftText = GetSocialPreviewText(value);
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(HasSelectedRepository));
            OnPropertyChanged(nameof(SelectedRepositoryUsedText));
            OnPropertyChanged(nameof(HasSelectedRepositoryUsedText));
            OnPropertyChanged(nameof(IsSelectedRepositoryDraftUsed));
            OnPropertyChanged(nameof(SelectedPriorityIndex));
            RaiseSelectedRepositoryCommandCanExecuteChanged();
            _ = LoadScreenshotPreviewsAsync(value);
        }
    }

    public string DraftText
    {
        get => m_draftText;
        set
        {
            if (!SetProperty(ref m_draftText, value))
                return;
            if (SelectedRepository != null)
                SelectedRepository.SocialPreviewText = value ?? string.Empty;
            OnPropertyChanged(nameof(IsSelectedRepositoryDraftUsed));
        }
    }

    public PostStyle SelectedPostStyle =>
        (PostStyle)Math.Clamp(SelectedPostStyleIndex, 0, PostStyleLabels.Length - 1);

    public string[] PostStyleLabels { get; } = ["Showcase", "Progress update", "Technical nugget", "Problem / solution", "Demo / video"];

    public int SelectedPostStyleIndex
    {
        get => m_selectedPostStyleIndex;
        set
        {
            if (!SetProperty(ref m_selectedPostStyleIndex, value))
                return;
            OnPropertyChanged(nameof(SelectedPostStyle));
            OnPropertyChanged(nameof(SelectedPostStyleText));
        }
    }

    public string SelectedPostStyleText => PostStyleLabels[(int)SelectedPostStyle];

    public string PostStyleTooltip =>
        "Choose the angle for the generated social text: intro, progress update, technical detail, problem/solution, or demo-focused.";

    public string PriorityTooltip =>
        "Priority controls the queue. High is promoted sooner, Normal is the default, and Excluded is removed from the priority queue.";

    public string[] PriorityLabels { get; } = ["High", "Normal", "Excluded"];

    public int SelectedPriorityIndex
    {
        get => GetPriorityIndex(SelectedRepository?.Priority ?? RepositoryPriority.Normal);
        set
        {
            var priority = GetPriority(value);
            if (SelectedRepository == null || SelectedRepository.Priority == priority)
                return;

            SelectedRepository.Priority = priority;
            OnPropertyChanged();
            SaveRepositoryState();
            StatusText = $"{SelectedRepository.Name} priority set to {priority}.";
        }
    }

    public string StatusText
    {
        get => m_statusText;
        private set => SetProperty(ref m_statusText, value);
    }

    public bool IsBusy
    {
        get => m_isBusy;
        private set => SetProperty(ref m_isBusy, value);
    }

    public bool IsSettingsOpen
    {
        get => m_isSettingsOpen;
        private set
        {
            if (!SetProperty(ref m_isSettingsOpen, value))
                return;
            OnPropertyChanged(nameof(IsRepositoryWorkspaceVisible));
        }
    }

    public bool IsRepositoryWorkspaceVisible => !IsSettingsOpen;

    internal bool IsStartupInitializationEnabled { get; set; } = true;

    public bool HasRepositories => Repositories.Count > 0;
    public bool HasSelectedRepository => SelectedRepository != null;
    public string[] SortModeLabels { get; } = ["Post priority", "Update date", "Name"];

    public int SortModeIndex
    {
        get => m_sortModeIndex;
        set
        {
            if (!SetProperty(ref m_sortModeIndex, value))
                return;
            ApplyRepositorySort();
        }
    }

    public string SelectedRepositoryUsedText
    {
        get
        {
            if (SelectedRepository == null)
                return string.Empty;

            var lastUsed = (m_settings.PromotionHistory ?? [])
                .Where(entry => entry.RepositoryName.Equals(SelectedRepository.Name, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.UsedAtUtc)
                .Where(usedAtUtc => usedAtUtc.HasValue)
                .OrderByDescending(usedAtUtc => usedAtUtc)
                .FirstOrDefault();
            return lastUsed.HasValue
                ? $"Last marked used {lastUsed.Value.ToLocalTime():yyyy-MM-dd HH:mm}"
                : string.Empty;
        }
    }

    public bool HasSelectedRepositoryUsedText => !string.IsNullOrWhiteSpace(SelectedRepositoryUsedText);

    public bool IsSelectedRepositoryDraftUsed =>
        SelectedRepository != null &&
        !string.IsNullOrWhiteSpace(DraftText) &&
        (m_settings.PromotionHistory ?? [])
        .Any(entry =>
            entry.UsedAtUtc.HasValue &&
            entry.RepositoryName.Equals(SelectedRepository.Name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.Text, DraftText, StringComparison.Ordinal));

    public string SettingsLocalRepositoryRootPath
    {
        get => m_settingsLocalRepositoryRootPath;
        set => SetProperty(ref m_settingsLocalRepositoryRootPath, value);
    }

    public string SettingsOpenAiApiKey
    {
        get => m_settingsOpenAiApiKey;
        set => SetProperty(ref m_settingsOpenAiApiKey, value);
    }

    public string SettingsOpenAiModel
    {
        get => m_settingsOpenAiModel;
        set => SetProperty(ref m_settingsOpenAiModel, value);
    }

    public ICommand CopyDraftCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand BrowseLocalRepositoryRootCommand { get; }
    public ICommand OpenOpenAiApiKeysCommand { get; }
    public ICommand RefreshRepositoriesCommand { get; }
    public ICommand TestOpenAiCommand { get; }
    public ICommand GenerateDraftCommand { get; }
    public ICommand GenerateDraftWithInstructionCommand { get; }
    public ICommand MarkSelectedDraftAsUsedCommand { get; }
    public ICommand OpenSelectedRepositoryCommand { get; }
    public ICommand OpenUrlCommand { get; }

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(m_settings.LocalRepositoryRootPath))
        {
            await LoadRepositoriesAsync();
            return;
        }

        OpenSettings();
        StatusText = "Choose your local repositories folder in Settings to load promotable projects.";
    }

    private Task LoadRepositoriesAsync()
    {
        if (string.IsNullOrWhiteSpace(m_settings.LocalRepositoryRootPath))
        {
            OpenSettings();
            StatusText = "Choose your local repositories folder in Settings first.";
            return Task.CompletedTask;
        }

        IsBusy = true;
        StatusText = "Scanning local repositories...";
        try
        {
            var previousState = m_settings.Repositories?
                .Where(repository => !string.IsNullOrWhiteSpace(repository.GitHubUrl))
                .GroupBy(repository => repository.GitHubUrl, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var repository = group.First();
                        return (repository.SocialPreviewText, repository.Priority);
                    },
                    StringComparer.OrdinalIgnoreCase) ??
                new Dictionary<string, (string SocialPreviewText, RepositoryPriority Priority)>(StringComparer.OrdinalIgnoreCase);

            Repositories.Clear();
            foreach (var repository in m_localRepositoryScanner.GetRepositories(new DirectoryInfo(m_settings.LocalRepositoryRootPath)))
            {
                if (previousState.TryGetValue(repository.GitHubUrl, out var state))
                {
                    repository.SocialPreviewText = state.SocialPreviewText;
                    repository.Priority = state.Priority;
                }
                Repositories.Add(repository);
            }
            ApplyRepositorySort();

            m_settings.Repositories = Repositories.ToList();
            m_settings.Save();
            SelectedRepository = Repositories.FirstOrDefault();
            StatusText = Repositories.Count == 0
                ? "No local repos with GitHub remotes found in that folder."
                : $"{Repositories.Count} local repos loaded.";
            OnPropertyChanged(nameof(HasRepositories));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText = $"Could not scan local repositories: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        return Task.CompletedTask;
    }

    private async Task CopyDraftAsync()
    {
        SaveRepositoryState();
        await m_clipboardService.CopyTextAsync(DraftText);
        StatusText = "Social preview copied to clipboard.";
    }

    private void OpenSettings()
    {
        LoadSettingsFields();
        IsSettingsOpen = true;
    }

    private async Task SaveSettingsAsync()
    {
        var localRepositoryRootPath = SettingsLocalRepositoryRootPath?.Trim() ?? string.Empty;
        var localRepositoryRootChanged = !string.Equals(m_settings.LocalRepositoryRootPath, localRepositoryRootPath, StringComparison.Ordinal);
        m_settings.LocalRepositoryRootPath = SettingsLocalRepositoryRootPath?.Trim() ?? string.Empty;
        m_settings.OpenAiModel = string.IsNullOrWhiteSpace(SettingsOpenAiModel) ? "gpt-5.4-mini" : SettingsOpenAiModel.Trim();
        m_settings.SetOpenAiApiKey(SettingsOpenAiApiKey);
        m_settings.Save();
        IsSettingsOpen = false;
        StatusText = "Settings saved.";

        if (localRepositoryRootChanged)
            await LoadRepositoriesAsync();
    }

    private async Task BrowseLocalRepositoryRootAsync()
    {
        var defaultFolder = string.IsNullOrWhiteSpace(SettingsLocalRepositoryRootPath)
            ? null
            : new DirectoryInfo(SettingsLocalRepositoryRootPath);
        var selectedFolder = await DialogService.Instance.SelectFolderAsync("Select local repositories folder", defaultFolder);
        if (selectedFolder != null)
            SettingsLocalRepositoryRootPath = selectedFolder.FullName;
    }

    private async Task TestOpenAiAsync()
    {
        var apiKey = SettingsOpenAiApiKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (!IsSettingsOpen)
                OpenSettings();
            StatusText = "Add an OpenAI API key in Settings first.";
            return;
        }

        var model = string.IsNullOrWhiteSpace(SettingsOpenAiModel) ? "gpt-5.4-mini" : SettingsOpenAiModel.Trim();

        IsBusy = true;
        StatusText = "Testing OpenAI connection...";
        try
        {
            var isConnected = await m_openAiPostGenerationService.TestConnectionAsync(apiKey, model);
            StatusText = isConnected ? "OpenAI connection OK." : "OpenAI responded, but the test reply was unexpected.";
        }
        catch (Exception exception) when (exception is HttpRequestException or ArgumentException or TaskCanceledException)
        {
            StatusText = $"OpenAI test failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GenerateDraftAsync()
    {
        await GenerateDraftAsync(null);
    }

    private async Task GenerateDraftWithInstructionAsync()
    {
        if (SelectedRepository == null)
            return;

        var extraInstruction = await DialogService.Instance.ShowTextEntryAsync(
            "Extra AI instruction",
            "Add a short instruction for this generation only.",
            watermark: "Optional direction for this generation",
            actionButton: "Generate",
            icon: MaterialIconKind.TextBoxEditOutline);
        if (extraInstruction == null)
            return;

        await GenerateDraftAsync(extraInstruction);
    }

    private async Task GenerateDraftAsync(string extraInstruction)
    {
        if (SelectedRepository == null || !await EnsureOpenAiKeyAsync())
            return;

        IsBusy = true;
        StatusText = string.IsNullOrWhiteSpace(extraInstruction)
            ? $"Generating social preview for {SelectedRepository.Name}..."
            : $"Generating social preview for {SelectedRepository.Name} with your instruction...";
        try
        {
            DraftText = await m_openAiPostGenerationService.GenerateDraftAsync(
                m_settings.GetOpenAiApiKey(),
                m_settings.OpenAiModel,
                SelectedRepository,
                SelectedPostStyle,
                extraInstruction,
                DraftText);
            SaveRepositoryState();
            StatusText = "Social preview generated. Review and edit before copying.";
        }
        catch (Exception exception) when (exception is HttpRequestException or ArgumentException or TaskCanceledException)
        {
            StatusText = $"OpenAI generation failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task<bool> EnsureOpenAiKeyAsync()
    {
        if (!string.IsNullOrWhiteSpace(m_settings.GetOpenAiApiKey()))
            return Task.FromResult(true);

        OpenSettings();
        StatusText = "Add an OpenAI API key in Settings first.";
        return Task.FromResult(false);
    }

    private void MarkSelectedDraftAsUsed()
    {
        if (SelectedRepository == null)
            return;

        PersistSelectedRepositoryDraft();
        var now = DateTime.UtcNow;
        m_settings.PromotionHistory ??= [];
        m_settings.PromotionHistory.Add(
            new PromotionHistoryEntry
            {
                RepositoryName = SelectedRepository.Name,
                Style = SelectedPostStyle,
                GeneratedAtUtc = now,
                UsedAtUtc = now,
                Text = DraftText ?? string.Empty
            });
        SaveRepositoryState();
        OnPropertyChanged(nameof(SelectedRepositoryUsedText));
        OnPropertyChanged(nameof(HasSelectedRepositoryUsedText));
        OnPropertyChanged(nameof(IsSelectedRepositoryDraftUsed));
        StatusText = $"{SelectedRepository.Name} marked as used.";
    }

    private void OpenSelectedRepository()
    {
        OpenUrl(SelectedRepository?.GitHubUrl);
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        uri.Open();
    }

    private static string GetSocialPreviewText(RepositoryProfile repository)
    {
        if (repository == null)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(repository.SocialPreviewText))
            repository.SocialPreviewText = CreateDraftText(repository);

        return repository.SocialPreviewText;
    }

    private static string CreateDraftText(RepositoryProfile repository)
    {
        if (repository == null)
            return string.Empty;

        var description = string.IsNullOrWhiteSpace(repository.Description)
            ? "a project from my GitHub archive"
            : repository.Description;
        var topicText = string.IsNullOrWhiteSpace(repository.TopicText)
            ? "#github #opensource"
            : $"#{repository.TopicText.Replace(", ", " #")}";
        return $"I built {repository.Name}: {description}\n\n{repository.GitHubUrl}\n\n{topicText}";
    }

    private void ApplyRepositorySort()
    {
        var selectedName = SelectedRepository?.Name;
        var sorted = GetSortedRepositories().ToArray();
        Repositories.Clear();
        foreach (var repository in sorted)
            Repositories.Add(repository);

        SelectedRepository =
            Repositories.FirstOrDefault(repository => repository.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase)) ??
            Repositories.FirstOrDefault();
    }

    private void PersistSelectedRepositoryDraft()
    {
        if (SelectedRepository != null)
            SelectedRepository.SocialPreviewText = DraftText ?? string.Empty;
    }

    private void SaveRepositoryState()
    {
        PersistSelectedRepositoryDraft();
        m_settings.Repositories = Repositories.ToList();
        m_settings.Save();
    }

    private IEnumerable<RepositoryProfile> GetSortedRepositories()
    {
        var mode = (RepositorySortMode)Math.Clamp(SortModeIndex, 0, SortModeLabels.Length - 1);
        return mode switch
        {
            RepositorySortMode.Name => Repositories.OrderBy(repository => repository.Name),
            RepositorySortMode.Updated => Repositories.OrderByDescending(repository => repository.UpdatedAtUtc).ThenBy(repository => repository.Name),
            _ => Repositories
                .OrderBy(repository => GetPrioritySortRank(repository.Priority))
                .ThenByDescending(repository => m_promotionScoreService.CalculateScore(repository, m_settings.PromotionHistory ?? [], DateTime.UtcNow))
                .ThenBy(repository => repository.Name)
        };
    }

    private static int GetPrioritySortRank(RepositoryPriority priority) =>
        priority switch
        {
            RepositoryPriority.High => 0,
            RepositoryPriority.Normal => 1,
            RepositoryPriority.Excluded => 2,
            _ => 1
        };

    private async Task LoadScreenshotPreviewsAsync(RepositoryProfile repository)
    {
        SelectedScreenshotPreviews.Clear();
        if (repository == null)
            return;

        foreach (var url in repository.ScreenshotUrls)
            SelectedScreenshotPreviews.Add(new ScreenshotPreviewViewModel(url));

        foreach (var preview in SelectedScreenshotPreviews.ToArray())
        {
            try
            {
                var imageBytes = await LoadImageBytesAsync(preview.Url);
                using var stream = new MemoryStream(imageBytes);
                preview.MarkImageLoaded(new Bitmap(stream), imageBytes);
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidDataException or ArgumentException or IOException or UnauthorizedAccessException)
            {
                preview.StatusText = "Could not load screenshot preview.";
            }
        }
    }

    private void LoadSettingsFields()
    {
        SettingsLocalRepositoryRootPath = m_settings.LocalRepositoryRootPath;
        SettingsOpenAiApiKey = m_settings.GetOpenAiApiKey();
        SettingsOpenAiModel = m_settings.OpenAiModel;
    }

    private void RaiseSelectedRepositoryCommandCanExecuteChanged()
    {
        (GenerateDraftCommand as CommandBase)?.RaiseCanExecuteChanged();
        (GenerateDraftWithInstructionCommand as CommandBase)?.RaiseCanExecuteChanged();
        (MarkSelectedDraftAsUsedCommand as CommandBase)?.RaiseCanExecuteChanged();
        (OpenSelectedRepositoryCommand as CommandBase)?.RaiseCanExecuteChanged();
    }

    private async Task<byte[]> LoadImageBytesAsync(string imageLocation)
    {
        if (Uri.TryCreate(imageLocation, UriKind.Absolute, out var uri) && !uri.IsFile)
            return await m_imageHttpClient.GetByteArrayAsync(uri);

        var localPath = Uri.TryCreate(imageLocation, UriKind.Absolute, out uri) && uri.IsFile
            ? uri.LocalPath
            : imageLocation;
        return await File.ReadAllBytesAsync(localPath);
    }

    private static int GetPriorityIndex(RepositoryPriority priority) =>
        priority switch
        {
            RepositoryPriority.High => 0,
            RepositoryPriority.Excluded => 2,
            _ => 1
        };

    private static RepositoryPriority GetPriority(int index) =>
        index switch
        {
            0 => RepositoryPriority.High,
            2 => RepositoryPriority.Excluded,
            _ => RepositoryPriority.Normal
        };
}
