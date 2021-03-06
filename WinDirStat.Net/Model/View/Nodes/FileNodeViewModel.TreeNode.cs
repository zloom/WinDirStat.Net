﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinDirStat.Net.TreeView;
using WinDirStat.Net.Utils;

namespace WinDirStat.Net.Model.View.Nodes {
	partial class FileNodeViewModel {

		private static readonly EmptyFileNodeCollection EmptyChildren = new EmptyFileNodeCollection();

		private FileNodeCollection children;
		internal FileNodeViewModel parent;
		private bool isVisible = true;
		/*private bool isHidden;
		private bool isSelected;
		private bool isExpanded;
		private bool isEditing;*/
		
		/*private BitField16 flags = new BitField16 {
			//[0] = true,
		};

#pragma warning disable 0649, IDE1006
		private bool isVisible {
			get => flags[0];
			set => flags[0] = value;
		}
		private bool isHidden {
			get => flags[1];
			set => flags[1] = value;
		}
		private bool isSelected {
			get => flags[2];
			set => flags[2] = value;
		}
		private bool isExpanded {
			get => flags[3];
			set => flags[3] = value;
		}
		private bool isEditing {
			get => flags[4];
			set => flags[4] = value;
		}
		private bool isEventsHooked {
			get => flags[5];
			set => flags[5] = value;
		}
#pragma warning restore 0649, IDE1006*/

		//private bool canExpandRecursively = true;
		//private bool lazyLoading;
		//private bool? isChecked;

		private void UpdateIsVisible(bool parentIsVisible, bool updateFlattener) {
			bool newIsVisible = parentIsVisible && !isHidden;
			if (isVisible != newIsVisible) {
				isVisible = newIsVisible;

				// invalidate the augmented data
				FileNodeViewModel node = this;
				while (node != null && node.totalListLength >= 0) {
					node.totalListLength = -1;
					node = node.listParent;
				}
				// Remember the removed nodes:
				List<FileNodeViewModel> removedNodes = null;
				if (updateFlattener && !newIsVisible) {
					removedNodes = VisibleDescendantsAndSelf().ToList();
				}
				// also update the model children:
				UpdateChildIsVisible(false);

				// Validate our invariants:
				if (updateFlattener)
					CheckRootInvariants();

				// Tell the flattener about the removed nodes:
				if (removedNodes != null) {
					var flattener = GetListRoot().treeFlattener;
					if (flattener != null) {
						flattener.NodesRemoved(GetVisibleIndexForNode(this), removedNodes);
						foreach (var n in removedNodes)
							n.OnIsVisibleChanged();
					}
				}
				// Tell the flattener about the new nodes:
				if (updateFlattener && newIsVisible) {
					var flattener = GetListRoot().treeFlattener;
					if (flattener != null) {
						flattener.NodesInserted(GetVisibleIndexForNode(this), VisibleDescendantsAndSelf());
						foreach (var n in VisibleDescendantsAndSelf())
							n.OnIsVisibleChanged();
					}
				}
			}
		}

		protected void OnIsVisibleChanged() { }

		private void UpdateChildIsVisible(bool updateFlattener) {
			if (children != null && children.Count > 0) {
				bool showChildren = isVisible && isExpanded;
				foreach (FileNodeViewModel child in children) {
					child.UpdateIsVisible(showChildren, updateFlattener);
				}
			}
		}

		#region Main
		
		public IFileNodeCollection Children {
			get {
				if (Model.IsContainerType) {
					if (children == null)
						children = new FileNodeCollection(this);
					return children;
				}
				return EmptyChildren;
			}
		}

		public FileNodeViewModel Parent {
			get => parent;
		}

		public Brush Foreground {
			get => SystemColors.WindowTextBrush;
		}

		public ImageSource Icon {
			get => icon;
		}

		public int Level {
			get => Parent != null ? Parent.Level + 1 : 0;
		}

		public bool IsRoot {
			get => Parent == null;
		}

		public bool IsHidden {
			get => isHidden;
			set {
				if (isHidden != value) {
					isHidden = value;
					if (parent != null)
						UpdateIsVisible(parent.isVisible && parent.isExpanded, true);
					AutoRaisePropertyChanged();
					if (Parent != null)
						Parent.RaisePropertyChanged(nameof(ShowExpander));
				}
			}
		}

		/// <summary>
		/// Return true when this node is not hidden and when all parent nodes are expanded and not hidden.
		/// </summary>
		public bool IsVisible {
			get => isVisible;
		}

		public bool IsSelected {
			get => isSelected;
			set {
				if (isSelected != value) {
					isSelected = value;
					AutoRaisePropertyChanged();
				}
			}
		}

		#endregion
		
		#region OnChildrenChanged
		internal protected void OnCollectionReset(List<FileNodeViewModel> newList, List<FileNodeViewModel> oldList) {
			foreach (FileNodeViewModel node in oldList) {
				Debug.Assert(node.parent == this);

				//if (node.isHidden)
				//	continue;

				node.parent = null;
				Debug.WriteLine("Removing {0} from {1}", node, this);
				FileNodeViewModel removeEnd = node;
				while (removeEnd.children != null && removeEnd.children.Count > 0)
					removeEnd = removeEnd.children.Last();

				List<FileNodeViewModel> removedNodes = null;
				int visibleIndexOfRemoval = 0;
				if (node.isVisible) {
					visibleIndexOfRemoval = GetVisibleIndexForNode(node);
					removedNodes = node.VisibleDescendantsAndSelf().ToList();
				}

				RemoveNodes(node, removeEnd);
			}

			FileNodeViewModel insertionPos = null;

			foreach (FileNodeViewModel node in newList) {
				Debug.Assert(node.Model.VirtualParent != null);
				Debug.Assert(node.parent == null);
				node.parent = this;
				node.UpdateIsVisible(isVisible && isExpanded, false);
				Debug.WriteLine("Inserting {0} after {1}", node, insertionPos);

				while (insertionPos != null && insertionPos.children != null && insertionPos.children.Count > 0) {
					insertionPos = insertionPos.children.Last();
				}
				InsertNodeAfter(insertionPos ?? this, node);

				insertionPos = node;
			}

			var flattener = GetListRoot().treeFlattener;
			if (flattener != null) {
				flattener.NodesReset();
			}

			RaisePropertyChanged(nameof(ShowExpander));
			if (newList[newList.Count - 1] != oldList[oldList.Count - 1]) {
				oldList[oldList.Count - 1].RaisePropertyChanged(nameof(IsLast));
				newList[newList.Count - 1].RaisePropertyChanged(nameof(IsLast));
			}
			//RaiseIsLastChangedIfNeeded(e);
		}
		internal protected void OnChildrenChanged(NotifyCollectionChangedEventArgs e) {
			//if (Name == "Users")
			Debug.WriteLine(e.Action);
			/*if (e.Action == NotifyCollectionChangedAction.Move) {
				foreach (FileNode node in e.OldItems) {
					if (node.parent == this)
						Debug.Assert(node.parent == this);

					//if (node.isHidden)
					//	continue;

					node.parent = null;
					Debug.WriteLine("Removing {0} from {1}", node, this);
					FileNode removeEnd = node;
					while (removeEnd.children != null && removeEnd.children.Count > 0)
						removeEnd = removeEnd.children.Last();

					List<FileNode> removedNodes = null;
					int visibleIndexOfRemoval = 0;
					if (node.isVisible) {
						visibleIndexOfRemoval = GetVisibleIndexForNode(node);
						removedNodes = node.VisibleDescendantsAndSelf().ToList();
					}

					RemoveNodes(node, removeEnd);
				}

				FileNode insertionPos;
				if (e.NewStartingIndex == 0)
					insertionPos = null;
				else
					insertionPos = children[e.NewStartingIndex - 1];

				foreach (FileNode node in e.NewItems) {
					if (node.parent == this)
						Debug.Assert(node.parent == this);
					node.parent = (FolderNode) this;
					node.UpdateIsVisible(isVisible && isExpanded, false);
					//Debug.WriteLine("Inserting {0} after {1}", node, insertionPos);

					while (insertionPos != null && insertionPos.children != null && insertionPos.children.Count > 0) {
						insertionPos = insertionPos.children.Last();
					}
					InsertNodeAfter(insertionPos ?? this, node);

					insertionPos = node;
				}

				var flattener = GetListRoot().treeFlattener;
				if (flattener != null) {
					flattener.NodesReset();
				}

				RaisePropertyChanged("ShowExpander");
				RaiseIsLastChangedIfNeeded(e);
				return;
			}*/
			if (e.OldItems != null) {
				foreach (FileNodeViewModel node in e.OldItems) {
					Debug.Assert(node.parent == this);

					//if (node.isHidden)
					//	continue;

					node.parent = null;
					Debug.WriteLine("Removing {0} from {1}", node, this);
					FileNodeViewModel removeEnd = node;
					while (removeEnd.children != null && removeEnd.children.Count > 0)
						removeEnd = removeEnd.children.Last();

					List<FileNodeViewModel> removedNodes = null;
					int visibleIndexOfRemoval = 0;
					if (node.isVisible) {
						visibleIndexOfRemoval = GetVisibleIndexForNode(node);
						removedNodes = node.VisibleDescendantsAndSelf().ToList();
					}

					RemoveNodes(node, removeEnd);

					if (removedNodes != null) {
						var flattener = GetListRoot().treeFlattener;
						if (flattener != null) {
							flattener.NodesRemoved(visibleIndexOfRemoval, removedNodes);
						}
					}
				}
			}
			if (e.NewItems != null) {
				FileNodeViewModel insertionPos;
				if (e.NewStartingIndex == 0)
					insertionPos = null;
				else
					insertionPos = children[e.NewStartingIndex - 1];

				foreach (FileNodeViewModel node in e.NewItems) {
					Debug.Assert(node.Model.VirtualParent != null);
					Debug.Assert(node.parent == null);
					node.parent = this;
					node.UpdateIsVisible(isVisible && isExpanded, false);
					Debug.WriteLine("Inserting {0} after {1}", node, insertionPos);

					while (insertionPos != null && insertionPos.children != null && insertionPos.children.Count > 0) {
						insertionPos = insertionPos.children.Last();
					}
					InsertNodeAfter(insertionPos ?? this, node);

					insertionPos = node;
					if (node.isVisible) {
						var flattener = GetListRoot().treeFlattener;
						if (flattener != null) {
							flattener.NodesInserted(GetVisibleIndexForNode(node), node.VisibleDescendantsAndSelf());
						}
					}
				}
			}

			RaisePropertyChanged(nameof(ShowExpander));
			RaiseIsLastChangedIfNeeded(e);
		}
		#endregion

		#region Expanding / LazyLoading
		
		public bool ShowExpander {
			get => model.HasVirtualChildren;
		}

		public bool IsExpanded {
			get => isExpanded;
			set {
				if (isExpanded != value) {
					isExpanded = value;
					if (isExpanded) {
						OnExpanding();
					}
					else {
						OnCollapsing();
					}
					UpdateChildIsVisible(true);
					AutoRaisePropertyChanged();
				}
			}
		}

		//protected virtual void OnExpanding() { }
		//protected virtual void OnCollapsing() { }

		/*public bool LazyLoading {
			get => lazyLoading;
			set {
				lazyLoading = value;
				if (lazyLoading) {
					IsExpanded = false;
					if (canExpandRecursively) {
						canExpandRecursively = false;
						RaisePropertyChanged("CanExpandRecursively");
					}
				}
				RaisePropertyChanged("LazyLoading");
				RaisePropertyChanged("ShowExpander");
			}
		}*/

		/// <summary>
		/// Gets whether this node can be expanded recursively.
		/// If not overridden, this property returns false if the node is using lazy-loading, and true otherwise.
		/// </summary>
		public bool CanExpandRecursively {
			get => false;// ItemCount <= 500;
		}

		/*public bool ShowIcon {
			get { return Icon != null; }
		}

		protected void LoadChildren() {
			throw new NotSupportedException(GetType().Name + " does not support lazy loading");
		}

		/// <summary>
		/// Ensures the children were initialized (loads children if lazy loading is enabled)
		/// </summary>
		public void EnsureLazyChildren() {
			if (LazyLoading) {
				LazyLoading = false;
				LoadChildren();
			}
		}*/

		#endregion

		#region Ancestors / Descendants

		public IEnumerable<FileNodeViewModel> Descendants() {
			return TreeTraversal.PreOrder(this.Children, n => n.Children);
		}

		public IEnumerable<FileNodeViewModel> VirtualDescendants() {
			return TreeTraversal.PreOrder(this.Children, n => n.Children);
		}

		public IEnumerable<FileNodeViewModel> DescendantsAndSelf() {
			return TreeTraversal.PreOrder(this, n => n.Children);
		}

		internal IEnumerable<FileNodeViewModel> VisibleDescendants() {
			return TreeTraversal.PreOrder(this.Children.Where(c => c.isVisible), n => n.Children.Where(c => c.isVisible));
		}

		internal IEnumerable<FileNodeViewModel> VisibleDescendantsAndSelf() {
			return TreeTraversal.PreOrder(this, n => n.Children.Where(c => c.isVisible));
		}

		public IEnumerable<FileNodeViewModel> Ancestors() {
			for (FileNodeViewModel n = this.Parent; n != null; n = n.Parent)
				yield return n;
		}

		public IEnumerable<FileNodeViewModel> AncestorsAndSelf() {
			for (FileNodeViewModel n = this; n != null; n = n.Parent)
				yield return n;
		}

		#endregion

		#region Editing

		public bool IsEditable {
			get => false;
		}

		public bool IsEditing {
			get => isEditing;
			set {
				if (isEditing != value) {
					isEditing = value;
					AutoRaisePropertyChanged();
				}
			}
		}

		public string LoadEditText() {
			return null;
		}

		public bool SaveEditText(string value) {
			return true;
		}

		#endregion

		#region Checkboxes (Disabled)

		/*public bool IsCheckable {
			get => false;
		}

		public bool? IsChecked {
			get => isChecked;
			set {
				SetIsChecked(value, true);
			}
		}

		void SetIsChecked(bool? value, bool update) {
			if (isChecked != value) {
				isChecked = value;

				if (update) {
					if (IsChecked != null) {
						foreach (var child in Descendants()) {
							if (child.IsCheckable) {
								child.SetIsChecked(IsChecked, false);
							}
						}
					}

					foreach (var parent in Ancestors()) {
						if (parent.IsCheckable) {
							if (!parent.TryValueForIsChecked(true)) {
								if (!parent.TryValueForIsChecked(false)) {
									parent.SetIsChecked(null, false);
								}
							}
						}
					}
				}

				RaisePropertyChanged("IsChecked");
			}
		}

		bool TryValueForIsChecked(bool? value) {
			if (Children.Where(n => n.IsCheckable).All(n => n.IsChecked == value)) {
				SetIsChecked(value, false);
				return true;
			}
			return false;
		}*/

		#endregion

		#region Cut / Copy / Paste / Delete (Disabled)

		/// <summary>
		/// Gets whether the node should render transparently because it is 'cut' (but not actually removed yet).
		/// </summary>
		/*public bool IsCut {
			get => false;
		}*/
		/*
			static List<WinDirNode> cuttedNodes = new List<WinDirNode>();
			static IDataObject cuttedData;
			static EventHandler requerySuggestedHandler; // for weak event
	
			static void StartCuttedDataWatcher()
			{
				requerySuggestedHandler = new EventHandler(CommandManager_RequerySuggested);
				CommandManager.RequerySuggested += requerySuggestedHandler;
			}
	
			static void CommandManager_RequerySuggested(object sender, EventArgs e)
			{
				if (cuttedData != null && !Clipboard.IsCurrent(cuttedData)) {
					ClearCuttedData();
				}
			}
	
			static void ClearCuttedData()
			{
				foreach (var node in cuttedNodes) {
					node.IsCut = false;
				}
				cuttedNodes.Clear();
				cuttedData = null;
			}
	
			//static public IEnumerable<WinDirNode> PurifyNodes(IEnumerable<WinDirNode> nodes)
			//{
			//    var list = nodes.ToList();
			//    var array = list.ToArray();
			//    foreach (var node1 in array) {
			//        foreach (var node2 in array) {
			//            if (node1.Descendants().Contains(node2)) {
			//                list.Remove(node2);
			//            }
			//        }
			//    }
			//    return list;
			//}
	
			bool isCut;
	
			public bool IsCut
			{
				get { return isCut; }
				private set
				{
					isCut = value;
					RaisePropertyChanged("IsCut");
				}
			}
	
			internal bool InternalCanCut()
			{
				return InternalCanCopy() && InternalCanDelete();
			}
	
			internal void InternalCut()
			{
				ClearCuttedData();
				cuttedData = Copy(ActiveNodesArray);
				Clipboard.SetDataObject(cuttedData);
	
				foreach (var node in ActiveNodes) {
					node.IsCut = true;
					cuttedNodes.Add(node);
				}
			}
	
			internal bool InternalCanCopy()
			{
				return CanCopy(ActiveNodesArray);
			}
	
			internal void InternalCopy()
			{
				Clipboard.SetDataObject(Copy(ActiveNodesArray));
			}
	
			internal bool InternalCanPaste()
			{
				return CanPaste(Clipboard.GetDataObject());
			}
	
			internal void InternalPaste()
			{
				Paste(Clipboard.GetDataObject());
	
				if (cuttedData != null) {
					DeleteCore(cuttedNodes.ToArray());
					ClearCuttedData();
				}
			}
		 */

		/*public bool CanDelete(WinDirNode[] nodes) {
			return false;
		}

		public void Delete(WinDirNode[] nodes) {
			throw new NotSupportedException(GetType().Name + " does not support deletion");
		}

		public void DeleteWithoutConfirmation(WinDirNode[] nodes) {
			throw new NotSupportedException(GetType().Name + " does not support deletion");
		}

		public bool CanCut(WinDirNode[] nodes) {
			return CanCopy(nodes) && CanDelete(nodes);
		}

		public void Cut(WinDirNode[] nodes) {
			var data = GetDataObject(nodes);
			if (data != null) {
				// TODO: default cut implementation should not immediately perform deletion, but use 'IsCut'
				Clipboard.SetDataObject(data, copy: true);
				DeleteWithoutConfirmation(nodes);
			}
		}

		public bool CanCopy(WinDirNode[] nodes) {
			return false;
		}

		public void Copy(WinDirNode[] nodes) {
			var data = GetDataObject(nodes);
			if (data != null)
				Clipboard.SetDataObject(data, copy: true);
		}

		protected IDataObject GetDataObject(WinDirNode[] nodes) {
			return null;
		}

		public bool CanPaste(IDataObject data) {
			return false;
		}

		public void Paste(IDataObject data) {
			throw new NotSupportedException(GetType().Name + " does not support copy/paste");
		}*/
		#endregion

		#region Drag and Drop (Disabled)
		/*public void StartDrag(DependencyObject dragSource, WinDirNode[] nodes) {
			// The default drag implementation works by reusing the copy infrastructure.
			// Derived classes should override this method
			var data = GetDataObject(nodes);
			if (data == null)
				return;
			DragDropEffects effects = DragDropEffects.Copy;
			if (CanDelete(nodes))
				effects |= DragDropEffects.Move;
			DragDropEffects result = DragDrop.DoDragDrop(dragSource, data, effects);
			if (result == DragDropEffects.Move) {
				DeleteWithoutConfirmation(nodes);
			}
		}

		/// <summary>
		/// Gets the possible drop effects.
		/// If the method returns more than one of (Copy|Move|Link), the tree view will choose one effect based
		/// on the allowed effects and keyboard status.
		/// </summary>
		public DragDropEffects GetDropEffect(DragEventArgs e, int index) {
			// Since the default drag implementation uses Copy(),
			// we'll use Paste() in our default drop implementation.
			if (CanPaste(e.Data)) {
				// If Ctrl is pressed -> copy
				// If moving is not allowed -> copy
				// Otherwise: move
				if ((e.KeyStates & DragDropKeyStates.ControlKey) != 0 || (e.AllowedEffects & DragDropEffects.Move) == 0)
					return DragDropEffects.Copy;
				return DragDropEffects.Move;
			}
			return DragDropEffects.None;
		}

		internal void InternalDrop(DragEventArgs e, int index) {
			if (LazyLoading) {
				EnsureLazyChildren();
				index = Children.Count;
			}

			Drop(e, index);
		}

		public void Drop(DragEventArgs e, int index) {
			// Since the default drag implementation uses Copy(),
			// we'll use Paste() in our default drop implementation.
			Paste(e.Data);
		}*/
		#endregion

		#region IsLast (for TreeView lines)

		public bool IsLast {
			get => Parent == null || Parent.Children[Parent.Children.Count - 1] == this;
		}

		void RaiseIsLastChangedIfNeeded(NotifyCollectionChangedEventArgs e) {
			switch (e.Action) {
			case NotifyCollectionChangedAction.Add:
				if (e.NewStartingIndex == Children.Count - 1) {
					if (Children.Count > 1) {
						Children[Children.Count - 2].RaisePropertyChanged("IsLast");
					}
					Children[Children.Count - 1].RaisePropertyChanged("IsLast");
				}
				break;
			case NotifyCollectionChangedAction.Remove:
				if (e.OldStartingIndex == Children.Count) {
					if (Children.Count > 0) {
						Children[Children.Count - 1].RaisePropertyChanged("IsLast");
					}
				}
				break;
			}
		}

		#endregion

		#region INotifyPropertyChanged Members

		/*public event PropertyChangedEventHandler PropertyChanged {
			add { }
			remove { }
			//add => PropertyChanged += value;
			//remove => PropertyChanged -= value;
		}*/
		/*public event PropertyChangedEventHandler PropertyChanged;

		protected internal void RaisePropertyChanged(string name) {
			//visualInfo?.RaisePropertyChanged(this, new PropertyChangedEventArgs(name));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		protected internal void RaisePropertiesChanged(params string[] names) {
			for (int i = 0; i < names.Length; i++)
				RaisePropertyChanged(names[i]);
		}

		protected internal void AutoRaisePropertyChanged([CallerMemberName] string name = null) {
			//visualInfo?.RaisePropertyChanged(this, new PropertyChangedEventArgs(name));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}*/

		#endregion

		#region Model (Disabled)
		/// <summary>
		/// Gets the underlying model object.
		/// </summary>
		/// <remarks>
		/// This property calls the virtual <see cref="GetModel()"/> helper method.
		/// I didn't make the property itself virtual because deriving classes
		/// may wish to replace it with a more specific return type,
		/// but C# doesn't support variance in override declarations.
		/// </remarks>
		/*public object Model {
			get => GetModel();
		}

		protected virtual object GetModel() {
			return null;
		}*/
		#endregion

		/// <summary>
		/// Gets called when the item is double-clicked.
		/// </summary>
		public void ActivateItem(RoutedEventArgs e) {
		}

		public void ShowContextMenu(ContextMenuEventArgs e) {
		}

		public override string ToString() {
			// used for keyboard navigation
			return Name ?? string.Empty;
		}
	}
}
