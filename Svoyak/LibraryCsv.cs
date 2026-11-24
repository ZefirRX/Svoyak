using System.Globalization;
using System.IO;
using System.Text;

namespace Svoyak;

internal static class LibraryCsv
{
    private const string GameRow = "Игра";
    private const string TopicRow = "Тема";
    private const string QuestionRow = "Вопрос";
    private static readonly string[] Header =
        ["Тип", "ID", "ID родителя", "Текст", "Описание или ответ", "Баллы", "Таймер (сек)", "Создано", "Изменено"];

    public static int Export(string path, GameRepository repository, TimerSettings timers)
    {
        if (!string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Сохраните файл с расширением .csv.");

        var games = repository.LoadGames();
        var temporaryPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, $"{Guid.NewGuid():N}.tmp");
        try
        {
            using (var writer = new StreamWriter(temporaryPath, append: false, new UTF8Encoding(true)))
            {
                WriteRow(writer, Header);
                int nextId = 0;
                foreach (var game in games)
                {
                    string gameId = Number(++nextId);
                    WriteRow(writer, GameRow, gameId, "", game.Title, game.Description, "", "", game.CreatedAt, game.UpdatedAt);
                    for (int topicIndex = 0; topicIndex < game.Topics.Count; topicIndex++)
                    {
                        var topic = game.Topics[topicIndex];
                        string topicId = Number(++nextId);
                        WriteRow(writer, TopicRow, topicId, gameId, topic.Title, "", "", "", "", "");
                        for (int questionIndex = 0; questionIndex < topic.Questions.Count; questionIndex++)
                        {
                            var question = topic.Questions[questionIndex];
                            int seconds = timers.Get(TimerSettings.Key(game.Id, topicIndex, questionIndex));
                            WriteRow(writer, QuestionRow, Number(++nextId), topicId, question.Text, question.Answer,
                                Number(question.Points), Number(seconds), "", "");
                        }
                    }
                }
            }
            File.Move(temporaryPath, path, overwrite: true);
            return games.Count;
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public static int Import(string path, GameRepository repository, TimerSettings timers)
    {
        using var reader = new StreamReader(path, new UTF8Encoding(false, true));
        var firstLine = reader.ReadLine() ?? "";
        char separator = firstLine.Contains(';') ? ';' : ',';
        if (!firstLine.Split(separator).Select(value => value.Trim('"')).SequenceEqual(Header))
            throw new InvalidDataException("Заголовки CSV не совпадают с форматом Svoyak. Используйте экспорт как шаблон.");

        var rows = new SortedDictionary<int, (int Number, string[] Fields)>();
        int recordNumber = 1;
        while (reader.Peek() >= 0)
        {
            var fields = ReadRow(reader, separator, ++recordNumber);
            if (fields is [""])
                continue;
            if (fields.Length != Header.Length || !TryNumber(fields[1], 1, out int id) ||
                !rows.TryAdd(id, (recordNumber, fields)))
                throw Error(recordNumber, "ожидаются девять столбцов и уникальный положительный ID.");
        }

        var games = new Dictionary<int, Game>();
        var topics = new Dictionary<int, Topic>();
        var durations = new Dictionary<Question, int>();
        foreach (var (id, (number, fields)) in rows)
        {
            string text = RestoreText(fields[3]);
            string extra = RestoreText(fields[4]);
            switch (fields[0])
            {
                case GameRow when fields[2].Length == 0 && !string.IsNullOrWhiteSpace(text):
                    games.Add(id, new Game
                    {
                        Title = text,
                        Description = extra.Length == 0 ? null : extra,
                        CreatedAt = RestoreText(fields[7]),
                        UpdatedAt = RestoreText(fields[8])
                    });
                    break;
                case TopicRow when TryNumber(fields[2], 1, out int gameId) && games.TryGetValue(gameId, out var game)
                    && !string.IsNullOrWhiteSpace(text):
                    var topic = new Topic { Title = text };
                    game.Topics.Add(topic);
                    topics.Add(id, topic);
                    break;
                case QuestionRow when TryNumber(fields[2], 1, out int topicId) && topics.TryGetValue(topicId, out var parent)
                    && TryNumber(fields[5], 0, out int points):
                    var question = new Question { Text = text, Answer = extra, Points = points };
                    if (fields[6].Length > 0)
                    {
                        if (!TryNumber(fields[6], 0, out int seconds) || seconds > 86400)
                            throw Error(number, "таймер должен быть целым числом от 0 до 86400 секунд.");
                        durations.Add(question, seconds);
                    }
                    parent.Questions.Add(question);
                    break;
                default:
                    throw Error(number, "проверьте тип записи, заголовок, ID родителя и баллы.");
            }
        }

        repository.Import(games.Values.ToList(), durations, timers);
        return games.Count;
    }

    private static string[] ReadRow(TextReader reader, char separator, int number)
    {
        var fields = new List<string>();
        int next;
        do
        {
            fields.Add(ReadField(reader, separator, number));
            next = reader.Read();
        } while (next == separator);

        if (next == '\r')
        {
            if (reader.Peek() == '\n')
                reader.Read();
        }
        else if (next is not ('\n' or -1))
            throw Error(number, "после закрывающей кавычки ожидается разделитель или конец строки.");
        return fields.ToArray();
    }

    private static string ReadField(TextReader reader, char separator, int number)
    {
        var value = new StringBuilder();
        if (reader.Peek() == '"')
        {
            reader.Read();
            for (int next = reader.Read(); next >= 0; next = reader.Read())
            {
                if (next == '"')
                {
                    if (reader.Peek() != '"')
                        return value.ToString();
                    reader.Read();
                }
                value.Append((char)next);
            }
            throw Error(number, "незакрытая кавычка.");
        }

        while (reader.Peek() is int next && next >= 0 && next != separator && next is not ('\r' or '\n'))
        {
            if (next == '"')
                throw Error(number, "кавычка внутри поля должна быть удвоена.");
            value.Append((char)reader.Read());
        }
        return value.ToString();
    }

    private static void WriteRow(TextWriter writer, params string?[] fields) =>
        writer.WriteLine(string.Join(';', fields.Select(value =>
        {
            var text = value ?? "";
            if (NeedsTextPrefix(text))
                text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        })));

    private static bool NeedsTextPrefix(string text) =>
        text.Length > 0 && (text[0] is '\'' or '\t' or '\r' or '\n' ||
            text.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@' or '＝' or '＋' or '－' or '＠');

    private static string RestoreText(string text) =>
        text.StartsWith('\'') && NeedsTextPrefix(text[1..]) ? text[1..] : text;

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static bool TryNumber(string text, int minimum, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= minimum;

    private static InvalidDataException Error(int number, string details) =>
        new($"Запись CSV №{number}: {details}");
}
