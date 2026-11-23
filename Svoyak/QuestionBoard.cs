using System.Windows;
using System.Windows.Controls;

namespace Svoyak;

internal static class QuestionBoard
{
    public static void Reset(Grid grid, IReadOnlyList<Topic> topics, int headerHeight, int questionHeight)
    {
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        if (topics.Count == 0) return;

        for (int column = 0; column < topics.Count; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(headerHeight) });
        int questionRows = topics.Max(topic => topic.Questions.Count);
        for (int row = 0; row < questionRows; row++)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(questionHeight) });
    }

    public static void AddCell(Grid grid, UIElement cell, int column, int row)
    {
        Grid.SetColumn(cell, column);
        Grid.SetRow(cell, row);
        grid.Children.Add(cell);
    }
}
