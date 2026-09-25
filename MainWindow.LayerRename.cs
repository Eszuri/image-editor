using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ImageEditor
{
    public partial class MainWindow
    {
        private readonly Dictionary<LayerItem, (TextBlock nameText, TextBox nameBox)> _layerRenameControls = new();
        private bool _isRenamingActive;

        public void RegisterLayerRenameControls(LayerItem item, TextBlock nameText, TextBox nameBox)
        {
            if (item == null) return;
            _layerRenameControls[item] = (nameText, nameBox);
        }

        public void StartRenameLayer(LayerItem? item)
        {
            if (item == null) return;

            // Commit any currently active rename first
            if (_isRenamingActive)
            {
                foreach (var kvp in _layerRenameControls)
                {
                    if (kvp.Value.nameBox.Visibility == Visibility.Visible)
                    {
                        CommitRenameLayer(kvp.Key, kvp.Value.nameBox.Text);
                        break;
                    }
                }
            }

            if (_layerRenameControls.TryGetValue(item, out var controls))
            {
                _isRenamingActive = true;
                controls.nameText.Visibility = Visibility.Collapsed;
                controls.nameBox.Visibility = Visibility.Visible;
                controls.nameBox.Text = item.Name;
                controls.nameBox.BringIntoView();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    controls.nameBox.Focus();
                    controls.nameBox.SelectAll();
                }), DispatcherPriority.Input);
            }
        }

        public void CommitRenameLayer(LayerItem? item, string newNameInput)
        {
            if (item == null) return;
            if (!_isRenamingActive) return;
            _isRenamingActive = false;

            if (_layerRenameControls.TryGetValue(item, out var controls))
            {
                controls.nameBox.Visibility = Visibility.Collapsed;
                controls.nameText.Visibility = Visibility.Visible;

                string oldName = item.Name;
                string trimmed = newNameInput?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(trimmed) || trimmed == oldName)
                {
                    controls.nameText.Text = oldName;
                    return;
                }

                string uniqueName = GetUniqueLayerName(trimmed, item);
                item.Name = uniqueName;
                controls.nameText.Text = uniqueName;

                _historyManager.Record(new RenameLayerAction(this, item, oldName, uniqueName));
                UpdateHistoryButtonStates();
            }
        }

        public void CancelRenameLayer(LayerItem? item)
        {
            if (item == null) return;
            _isRenamingActive = false;

            if (_layerRenameControls.TryGetValue(item, out var controls))
            {
                controls.nameBox.Visibility = Visibility.Collapsed;
                controls.nameText.Visibility = Visibility.Visible;
                controls.nameBox.Text = item.Name;
            }
        }

        public void InternalSetLayerName(LayerItem layer, string name)
        {
            if (layer == null) return;
            layer.Name = name;
            if (_layerRenameControls.TryGetValue(layer, out var controls))
            {
                controls.nameText.Text = name;
                controls.nameBox.Text = name;
            }
            else
            {
                UpdateLayerListUI();
            }
        }

        private void LayerRename_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLayerItem != null)
            {
                StartRenameLayer(_selectedLayerItem);
            }
        }
    }
}
