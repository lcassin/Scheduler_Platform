using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using PrintDialog = System.Windows.Controls.PrintDialog;

namespace MermaidEditor;

public partial class PrintCodePreviewDialog : Window
{
    private readonly string _code;
    private readonly string _documentTitle;
    private double _fontSize = 10;
    private FlowDocument? _flowDocument;

    public PrintCodePreviewDialog(string code, string documentTitle)
    {
        InitializeComponent();
        _code = code;
        _documentTitle = documentTitle;
        
        Loaded += PrintCodePreviewDialog_Loaded;
    }

    private void PrintCodePreviewDialog_Loaded(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontSizeCombo.SelectedItem is ComboBoxItem item)
        {
            var content = item.Content.ToString();
            _fontSize = content switch
            {
                "8 pt" => 8,
                "9 pt" => 9,
                "10 pt" => 10,
                "11 pt" => 11,
                "12 pt" => 12,
                _ => 10
            };
            UpdatePreview();
        }
    }

    private void UpdatePreview()
    {
        if (PreviewViewer == null) return;

        // Create FlowDocument for preview
        _flowDocument = new FlowDocument
        {
            PagePadding = new Thickness(50),
            ColumnWidth = double.MaxValue,
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New, monospace"),
            FontSize = _fontSize
        };

        // Add title
        var titleParagraph = new Paragraph(new Run(_documentTitle))
        {
            FontSize = _fontSize + 4,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 20),
            Foreground = System.Windows.Media.Brushes.Black
        };
        _flowDocument.Blocks.Add(titleParagraph);

        // Add code lines
        var lines = _code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            var paragraph = new Paragraph(new Run(string.IsNullOrEmpty(line) ? " " : line))
            {
                FontSize = _fontSize,
                Margin = new Thickness(0, 0, 0, 2),
                TextAlignment = System.Windows.TextAlignment.Left,
                Foreground = System.Windows.Media.Brushes.Black
            };
            _flowDocument.Blocks.Add(paragraph);
        }

        PreviewViewer.Document = _flowDocument;

        // Update line count
        if (LineCountText != null)
            LineCountText.Text = $"Lines: {lines.Length}";
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var printDialog = new PrintDialog();
        
        if (printDialog.ShowDialog() == true)
        {
            if (_flowDocument != null)
            {
                // Set page size for printing
                _flowDocument.PageWidth = printDialog.PrintableAreaWidth;
                _flowDocument.PageHeight = printDialog.PrintableAreaHeight;

                // Create a DocumentPaginator for the FlowDocument
                var paginator = ((IDocumentPaginatorSource)_flowDocument).DocumentPaginator;
                paginator.PageSize = new System.Windows.Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);

                // Print the document
                printDialog.PrintDocument(paginator, $"Code: {_documentTitle}");
            }
            
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
