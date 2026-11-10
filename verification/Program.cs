using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Svoyak;
class Program
{
 [STAThread] static void Main()
 {
  File.Copy(Path.GetFullPath("../SQLite_Svoyak.db"), Path.Combine(AppContext.BaseDirectory,"SQLite_Svoyak.db"),true);
  var repository = new GameRepository();
  var game = new Game { Title="Общая эрудиция", Topics=[new Topic { Title="История", Questions=[new Question { Text="В каком году началась Первая мировая война?", Answer="1914", Points=100 }] }] };
  repository.Save(game); repository.Save(game);
  var stored=repository.LoadGames().Single(g=>g.Id==game.Id);
  if(stored.Topics.Count!=1 || stored.Topics[0].Questions.Count!=1) throw new Exception("Save failed");
  var app=new App(); app.InitializeComponent();
  var window=new MainWindow(); window.Show(); Render(window,"menu");
  Invoke(window,"EditGame",stored); Render(window,"editor");
  Invoke(window,"OpenSetup",window,new RoutedEventArgs()); Render(window,"setup");
  typeof(MainWindow).GetField("currentGame",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,stored);
  Invoke(window,"ReturnToBoard",window,new RoutedEventArgs()); Render(window,"board");
  stored.Topics[0].Questions[0].MediaType = 1;
  stored.Topics[0].Questions[0].MediaPath = Path.GetFullPath("../Svoyak/Assets/logo.png");
  Invoke(window,"OpenQuestion",stored.Topics[0],stored.Topics[0].Questions[0]); Render(window,"question");
  var team = new Team { Name = "Проверка" };
  Invoke(window,"RegisterWrongAnswer",team,stored.Topics[0].Questions[0]);
  Invoke(window,"RegisterWrongAnswer",team,stored.Topics[0].Questions[0]);
  if(team.Score != -100) throw new Exception("Wrong-answer penalty must apply once");
  Invoke(window,"UndoScore",window,new RoutedEventArgs());
  if(team.Score != 0) throw new Exception("Penalty undo failed");
  Invoke(window,"RegisterWrongAnswer",team,stored.Topics[0].Questions[0]);
  if(team.Score != -100) throw new Exception("Penalty after undo failed");
  Invoke(window,"OpenProjector",window,new RoutedEventArgs());
  var audience = (Window)typeof(MainWindow).GetField("projector",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
  if(audience == null || !audience.IsVisible) throw new Exception("Audience window did not open");
  var imageQuestion = stored.Topics[0].Questions[0];
  ((ProjectorWindow)audience).Update(stored.Title, stored.Topics[0].Title, imageQuestion, 30, new Team[] { team });
  Render(audience,"audience-image");
  var image = (System.Windows.Controls.Image)audience.FindName("QuestionPicture");
  if(image.Source == null || image.ActualHeight <= 0) throw new Exception("Audience image missing");
  ((ProjectorWindow)audience).Update(stored.Title,"",null,0,new Team[] { team }, stored,
    new System.Collections.Generic.HashSet<int> { imageQuestion.Id });
  Render(audience,"audience-board");
  var board = (System.Windows.Controls.Grid)audience.FindName("AudienceBoard");
  if(board.Children.Count != 2 || board.IsHitTestVisible) throw new Exception("Audience board must be visible and non-interactive");
  var cell = (System.Windows.Controls.Border)board.Children[1];
  var label = (System.Windows.Controls.TextBlock)cell.Child;
  if(label.Text != "100" || label.TextDecorations != TextDecorations.Strikethrough) throw new Exception("Played cell missing");
  if(((FrameworkElement)audience.FindName("QuestionView")).Visibility != Visibility.Collapsed) throw new Exception("Question must hide on board");
  if(image.Source != null) throw new Exception("Stale audience image");
  audience.Close();
  Invoke(window,"EditGame",stored);
  Invoke(window,"DeleteTopic",window,new RoutedEventArgs());
  var draft = (Game)typeof(MainWindow).GetField("draft",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
  if(draft.Topics.Count != 0) throw new Exception("Topic deletion failed");
  Invoke(window,"AddTopic",window,new RoutedEventArgs());
  if(draft.Topics.Count != 1) throw new Exception("Adding after deletion failed");
  Invoke(window,"OpenLibrary",window,new RoutedEventArgs()); Render(window,"library");
  Invoke(window,"FinishGame",window,new RoutedEventArgs()); Render(window,"results");
  window.Width=1050; window.Height=680;
  Invoke(window,"OpenSetup",window,new RoutedEventArgs());
  for(int index=0;index<6;index++) Invoke(window,"AddTeam",window,new RoutedEventArgs());
  Render(window,"setup-compact-eight");
  Invoke(window,"EditGame",stored); Render(window,"editor-compact");
  Invoke(window,"OpenQuestion",stored.Topics[0],stored.Topics[0].Questions[0]); Render(window,"question-compact");
  window.Close(); app.Shutdown(); Console.WriteLine("PASS: save/update/load, five screens, wrong-answer penalty, duplicate prevention, undo");
 }
 static void Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
 static void Render(Window window,string name)
 {
  window.UpdateLayout(); var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(window);
  var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file=File.Create(name+".png"); encoder.Save(file);
 }
}
