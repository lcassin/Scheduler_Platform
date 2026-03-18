using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;

namespace MermaidEditor;

public partial class AskAiDialog : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    private readonly AiService _aiService = new();
    private readonly List<ChatMessage> _conversationHistory = new();
    private readonly List<ImageAttachment> _pendingAttachments = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private string _lastAssistantResponse = "";
    private bool _isStreaming;

    /// <summary>
    /// The current editor content, set by the caller before showing the dialog.
    /// </summary>
    public string EditorContent { get; set; } = "";

    /// <summary>
    /// The current file type (Mermaid or Markdown), set by the caller.
    /// </summary>
    public string FileType { get; set; } = "Mermaid";

    /// <summary>
    /// Text to insert into the editor when the user clicks "Insert at Cursor".
    /// Set after the dialog closes if DialogResult is true.
    /// </summary>
    public string TextToInsert { get; private set; } = "";

    /// <summary>
    /// When true, the caller should replace the entire editor content instead of inserting at cursor.
    /// Set when the user clicks "Replace Diagram" in visual editor mode.
    /// </summary>
    public bool ShouldReplaceDiagram { get; private set; }

    /// <summary>
    /// Whether the visual editor is currently active. Set by the caller.
    /// Enables diagram-type-aware prompting and the "Replace Diagram" button.
    /// </summary>
    public bool IsVisualEditorMode { get; set; }

    /// <summary>
    /// The active diagram type name (e.g., "Flowchart", "Sequence", "Class", "State", "ER").
    /// Used to tailor the AI system prompt for diagram generation.
    /// </summary>
    public string DiagramType { get; set; } = "Flowchart";

    public AskAiDialog()
    {
        InitializeComponent();
        SourceInitialized += AskAiDialog_SourceInitialized;
        Loaded += AskAiDialog_Loaded;
        Closed += AskAiDialog_Closed;
    }

    private void AskAiDialog_SourceInitialized(object? sender, EventArgs e)
    {
        UpdateTitleBarTheme();
    }

    private void UpdateTitleBarTheme()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                var isDark = ThemeManager.IsDarkTheme;
                int darkModeValue = isDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(int));

                var colors = ThemeManager.GetThemeColors(ThemeManager.CurrentTheme);
                int captionColor = (colors.Background.B << 16) | (colors.Background.G << 8) | colors.Background.R;
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
            }
        }
        catch
        {
            // Silently fail if DWM API is not available
        }
    }

    private void AskAiDialog_Loaded(object sender, RoutedEventArgs e)
    {
        // Validate AI configuration
        var settings = SettingsManager.Current;
        if (string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            AddSystemMessage("No API key configured. Please set your AI API key in Settings (View > Settings > AI tab) to use this feature.");
            SendButton.IsEnabled = false;
            PromptInput.IsEnabled = false;
        }
        else
        {
            if (IsVisualEditorMode)
            {
                AddSystemMessage($"Connected to {settings.AiProvider} ({settings.AiModel}). Visual Editor mode ({DiagramType} diagram).\nDescribe the diagram you want to generate, or ask for modifications to the current diagram.");
                ReplaceDiagramButton.Visibility = Visibility.Visible;
            }
            else
            {
                AddSystemMessage($"Connected to {settings.AiProvider} ({settings.AiModel}). Type your message below.");
            }
            PromptInput.Focus();
        }
    }

    private void AskAiDialog_Closed(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _aiService.Dispose();
    }

    // ── Message Display ──

    private void AddSystemMessage(string text)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 4, 40, 4),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };

        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0x9C, 0x9C)),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontStyle = FontStyles.Italic
        };

        border.Child = textBlock;
        ChatMessagesPanel.Children.Add(border);
        ScrollToBottom();
    }

    private void AddUserMessage(string text, int attachmentCount = 0)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x09, 0x47, 0x71)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(40, 4, 0, 4),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var panel = new StackPanel();

        if (attachmentCount > 0)
        {
            var attachInfo = new TextBlock
            {
                Text = $"[{attachmentCount} file(s) attached]",
                Foreground = new SolidColorBrush(Color.FromRgb(0x7A, 0xB8, 0xE8)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.Children.Add(attachInfo);
        }

        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF1, 0xF1, 0xF1)),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13
        };
        panel.Children.Add(textBlock);

        border.Child = panel;
        ChatMessagesPanel.Children.Add(border);
        ScrollToBottom();
    }

    private Border CreateAssistantMessageBubble()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 4, 40, 4),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };

        var textBlock = new TextBlock
        {
            Text = "",
            Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13
        };

        border.Child = textBlock;
        ChatMessagesPanel.Children.Add(border);
        return border;
    }

    private void AppendToAssistantBubble(Border bubble, string token)
    {
        if (bubble.Child is TextBlock textBlock)
        {
            textBlock.Text += token;
        }
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        ChatScrollViewer.ScrollToEnd();
    }

    // ── Attachment Management ──

    private void AttachFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Attach File",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var filePath in dialog.FileNames)
            {
                try
                {
                    var fileBytes = File.ReadAllBytes(filePath);
                    var base64 = Convert.ToBase64String(fileBytes);
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();
                    var mediaType = ext switch
                    {
                        ".png" => "image/png",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".gif" => "image/gif",
                        ".bmp" => "image/bmp",
                        ".webp" => "image/webp",
                        _ => "application/octet-stream"
                    };

                    // If it's not an image, read as text and include as text content
                    if (mediaType == "application/octet-stream")
                    {
                        // Try to read as text file
                        try
                        {
                            var textContent = File.ReadAllText(filePath);
                            _pendingAttachments.Add(new ImageAttachment
                            {
                                Base64Data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(textContent)),
                                MediaType = "text/plain",
                                FileName = Path.GetFileName(filePath)
                            });
                        }
                        catch
                        {
                            _pendingAttachments.Add(new ImageAttachment
                            {
                                Base64Data = base64,
                                MediaType = mediaType,
                                FileName = Path.GetFileName(filePath)
                            });
                        }
                    }
                    else
                    {
                        _pendingAttachments.Add(new ImageAttachment
                        {
                            Base64Data = base64,
                            MediaType = mediaType,
                            FileName = Path.GetFileName(filePath)
                        });
                    }
                }
                catch (Exception ex)
                {
                    AddSystemMessage($"Failed to attach {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            UpdateAttachmentsDisplay();
        }
    }

    private void UpdateAttachmentsDisplay()
    {
        AttachmentsPanel.Children.Clear();

        if (_pendingAttachments.Count == 0)
        {
            AttachmentsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        AttachmentsPanel.Visibility = Visibility.Visible;

        foreach (var attachment in _pendingAttachments)
        {
            var chip = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 4, 4),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            var icon = new TextBlock
            {
                Text = attachment.MediaType.StartsWith("image/") ? "\uEB9F" : "\uE8A5",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0x9C, 0x9C)),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var name = new TextBlock
            {
                Text = attachment.FileName,
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 150,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var removeBtn = new Button
            {
                Content = new TextBlock
                {
                    Text = "\uE711",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0x9C, 0x9C))
                },
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand,
                Tag = attachment
            };
            removeBtn.Click += RemoveAttachment_Click;

            panel.Children.Add(icon);
            panel.Children.Add(name);
            panel.Children.Add(removeBtn);
            chip.Child = panel;
            AttachmentsPanel.Children.Add(chip);
        }
    }

    private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ImageAttachment attachment)
        {
            _pendingAttachments.Remove(attachment);
            UpdateAttachmentsDisplay();
        }
    }

    // ── Send / Stream ──

    private void PromptInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            if (!_isStreaming && SendButton.IsEnabled)
            {
                Send_Click(sender, e);
            }
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var prompt = PromptInput.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        var settings = SettingsManager.Current;
        if (string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            AddSystemMessage("No API key configured. Please set your AI API key in Settings.");
            return;
        }

        // Show user message
        AddUserMessage(prompt, _pendingAttachments.Count);
        PromptInput.Text = "";

        // Build the message
        var userMessage = new ChatMessage { Role = "user" };

        // Build text content with optional editor context
        var textParts = new List<string>();
        if (IncludeEditorContentCheck.IsChecked == true && !string.IsNullOrWhiteSpace(EditorContent))
        {
            textParts.Add($"[Current {FileType} editor content]\n```\n{EditorContent}\n```\n");
        }

        // Include text file attachments inline
        var imageAttachments = new List<ImageAttachment>();
        foreach (var att in _pendingAttachments)
        {
            if (att.MediaType == "text/plain")
            {
                var textContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(att.Base64Data));
                textParts.Add($"[Attached file: {att.FileName}]\n```\n{textContent}\n```\n");
            }
            else if (att.MediaType.StartsWith("image/"))
            {
                imageAttachments.Add(att);
            }
        }

        textParts.Add(prompt);
        userMessage.TextContent = string.Join("\n", textParts);

        if (imageAttachments.Count > 0)
        {
            userMessage.ImageData = imageAttachments;
        }

        // Clear attachments after sending
        _pendingAttachments.Clear();
        UpdateAttachmentsDisplay();

        // Add system message if this is the first message
        if (_conversationHistory.Count == 0)
        {
            _conversationHistory.Add(new ChatMessage
            {
                Role = "system",
                TextContent = BuildSystemPrompt()
            });
        }

        _conversationHistory.Add(userMessage);

        // Start streaming
        _isStreaming = true;
        SendButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        InsertButton.IsEnabled = false;
        StatusText.Text = "Generating...";
        _lastAssistantResponse = "";

        var bubble = CreateAssistantMessageBubble();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            await _aiService.StreamChatAsync(
                _conversationHistory,
                settings.AiProvider,
                settings.AiApiKey,
                settings.AiModel,
                settings.AiEndpoint,
                token =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _lastAssistantResponse += token;
                        AppendToAssistantBubble(bubble, token);
                    });
                },
                _cancellationTokenSource.Token);

            // Add assistant response to history
            _conversationHistory.Add(new ChatMessage
            {
                Role = "assistant",
                TextContent = _lastAssistantResponse
            });

            StatusText.Text = "Done";
            var hasResponse = !string.IsNullOrEmpty(_lastAssistantResponse);
            InsertButton.IsEnabled = hasResponse;
            if (IsVisualEditorMode)
                ReplaceDiagramButton.IsEnabled = hasResponse;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Stopped";
            AppendToAssistantBubble(bubble, "\n[Stopped]");
        }
        catch (AiServiceException ex)
        {
            StatusText.Text = "Error";
            AddSystemMessage($"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error";
            AddSystemMessage($"Unexpected error: {ex.Message}");
        }
        finally
        {
            _isStreaming = false;
            SendButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }

    private void ClearChat_Click(object sender, RoutedEventArgs e)
    {
        _conversationHistory.Clear();
        ChatMessagesPanel.Children.Clear();
        _lastAssistantResponse = "";
        InsertButton.IsEnabled = false;
        ReplaceDiagramButton.IsEnabled = false;
        StatusText.Text = "";

        var settings = SettingsManager.Current;
        if (!string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            AddSystemMessage($"Chat cleared. Connected to {settings.AiProvider} ({settings.AiModel}).");
        }
    }

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastAssistantResponse))
        {
            // Extract code blocks if present, otherwise use full response
            TextToInsert = ExtractCodeContent(_lastAssistantResponse);
            ShouldReplaceDiagram = false;
            DialogResult = true;
            Close();
        }
    }

    private void ReplaceDiagram_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastAssistantResponse))
        {
            // Extract code blocks if present, otherwise use full response
            TextToInsert = ExtractCodeContent(_lastAssistantResponse);
            ShouldReplaceDiagram = true;
            DialogResult = true;
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Builds the system prompt based on the current mode (text editor vs visual editor).
    /// </summary>
    private string BuildSystemPrompt()
    {
        if (IsVisualEditorMode)
        {
            var diagramExamples = DiagramType switch
            {
                "Flowchart" => "flowchart TD\n    A[Start] --> B{Decision}\n    B -->|Yes| C[Action]\n    B -->|No| D[End]",
                "Sequence" => "sequenceDiagram\n    Alice->>Bob: Hello\n    Bob-->>Alice: Hi back",
                "Class" => "classDiagram\n    class Animal {\n        +String name\n        +makeSound()\n    }\n    Animal <|-- Dog",
                "State" => "stateDiagram-v2\n    [*] --> Idle\n    Idle --> Processing : start\n    Processing --> Done : complete\n    Done --> [*]",
                "ER" => "erDiagram\n    CUSTOMER ||--o{ ORDER : places\n    ORDER ||--|{ LINE-ITEM : contains",
                _ => "flowchart TD\n    A[Start] --> B[End]"
            };

            return $"You are an AI assistant integrated into a Mermaid diagram visual editor. " +
                $"The user is working with a {DiagramType} diagram. " +
                $"When generating or modifying diagrams, ALWAYS output valid Mermaid {DiagramType.ToLowerInvariant()} syntax in a code block. " +
                $"Example format:\n```mermaid\n{diagramExamples}\n```\n" +
                "Important rules:\n" +
                "- Always wrap Mermaid code in a ```mermaid code block\n" +
                "- Generate COMPLETE diagrams (not fragments) when the user asks for a new diagram\n" +
                "- When modifying, include the full updated diagram\n" +
                "- Use descriptive node labels and meaningful relationship labels\n" +
                "- Be concise in explanations but thorough in diagram code";
        }

        return $"You are a helpful AI assistant integrated into a code editor. The user is working with {FileType} files. " +
            "Help them with writing, editing, explaining, and generating content. " +
            "When providing code or content to insert, format it clearly. " +
            "Be concise and helpful.";
    }

    /// <summary>
    /// Extracts content from code blocks (```...```) if present.
    /// If no code blocks are found, returns the full text.
    /// </summary>
    private static string ExtractCodeContent(string text)
    {
        var codeBlocks = new List<string>();
        int searchFrom = 0;

        while (true)
        {
            int start = text.IndexOf("```", searchFrom, StringComparison.Ordinal);
            if (start < 0) break;

            // Skip the opening ``` and optional language identifier
            int contentStart = text.IndexOf('\n', start);
            if (contentStart < 0) break;
            contentStart++; // Skip the newline

            int end = text.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (end < 0) break;

            codeBlocks.Add(text.Substring(contentStart, end - contentStart).TrimEnd());
            searchFrom = end + 3;
        }

        return codeBlocks.Count > 0 ? string.Join("\n\n", codeBlocks) : text;
    }
}
