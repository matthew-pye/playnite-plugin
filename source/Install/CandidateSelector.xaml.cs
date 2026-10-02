using Graviton.Models.Install;

using Playnite;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Graviton.Install
{
    public partial class CandidateSelector : UserControl
    {
        public string Category { get; set; } = string.Empty;
        public InstallMode InstallMode { get; set; }

        public ObservableCollection<UpdateDLCCandidate> Candidates { get; set; }

        public bool Cancelled = true;

        private Point _dragStartPoint;
        private UpdateDLCCandidate? _draggedItem;
        private int _lastDragTargetIndex = -1;

        public CandidateSelector(List<UpdateDLCCandidate> candidates, InstallMode mode, string category)
        {
            InitializeComponent();

            Candidates = candidates.ToObservableCollection();
            InstallMode = mode;
            Category = category;
            MainGrid.DataContext = this;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this)?.Close();
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            Cancelled = false;
            Window.GetWindow(this)?.Close();
        }

        private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not UpdateDLCCandidate candidate)
                return;
            
            _dragStartPoint = e.GetPosition(CandidateListbox);
            _draggedItem = candidate;
        }

        private void CandidateList_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null || InstallMode != InstallMode.Sequential)
                return;

            var currentPosition = e.GetPosition(CandidateListbox);
            var difference = _dragStartPoint - currentPosition;

            if (Math.Abs(difference.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(difference.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            try
            {
                DragDrop.DoDragDrop(CandidateListbox, _draggedItem, DragDropEffects.Move);
            }
            finally
            {
                _draggedItem = null;
                _lastDragTargetIndex = -1;
            }
        }

        private void CandidateList_DragOver(object sender, DragEventArgs e)
        {
            if (InstallMode != InstallMode.Sequential || !e.Data.GetDataPresent(typeof(UpdateDLCCandidate)))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var dragged = e.Data.GetData(typeof(UpdateDLCCandidate)) as UpdateDLCCandidate;
            if (dragged == null)
                return;

            var element = e.OriginalSource as DependencyObject;
            var targetContainer = ItemsControl.ContainerFromElement(CandidateListbox, element) as ListBoxItem;

            if (targetContainer?.DataContext is not UpdateDLCCandidate target)
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            var oldIndex = Candidates.IndexOf(dragged);
            var newIndex = Candidates.IndexOf(target);

            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            if (_lastDragTargetIndex != newIndex)
            {
                Candidates.Move(oldIndex, newIndex);
                _lastDragTargetIndex = newIndex;
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void CandidateList_Drop(object sender, DragEventArgs e)
        {
            _lastDragTargetIndex = -1;
            _draggedItem = null;
        }
    }
}