using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Internal;
using UnityEngine.Scripting;

namespace UnityEditor
{
	[UsedByNativeCode]
	public class EditorWindow : ScriptableObject
	{
		[HideInInspector, SerializeField]
		private bool m_AutoRepaintOnSceneChange;

		[HideInInspector, SerializeField]
		private Vector2 m_MinSize = new Vector2(100f, 100f);

		[HideInInspector, SerializeField]
		private Vector2 m_MaxSize = new Vector2(4000f, 4000f);

		[HideInInspector, SerializeField]
		internal GUIContent m_TitleContent;

		[HideInInspector, SerializeField]
		private int m_DepthBufferBits = 0;

		[HideInInspector, SerializeField]
		internal Rect m_Pos = new Rect(0f, 0f, 320f, 240f);

		private VisualContainer m_RootVisualContainer;

		private Rect m_GameViewRect;

		private Rect m_GameViewClippedRect;

		private Vector2 m_GameViewTargetSize;

		private bool m_DontClearBackground;

		private EventInterests m_EventInterests;

		[NonSerialized]
		internal HostView m_Parent;

		private const double kWarningFadeoutWait = 4.0;

		private const double kWarningFadeoutTime = 1.0;

		internal GUIContent m_Notification = null;

		private Vector2 m_NotificationSize;

		internal float m_FadeoutTime = 0f;

		internal VisualContainer rootVisualContainer
		{
			get
			{
				if (this.m_RootVisualContainer == null)
				{
					this.m_RootVisualContainer = new VisualContainer
					{
						name = VisualElementUtils.GetUniqueName("rootVisualContainer"),
						pickingMode = PickingMode.Ignore
					};
				}
				return this.m_RootVisualContainer;
			}
		}

		public bool wantsMouseMove
		{
			get
			{
				return this.m_EventInterests.wantsMouseMove;
			}
			set
			{
				this.m_EventInterests.wantsMouseMove = value;
				this.MakeParentsSettingsMatchMe();
			}
		}

		public bool wantsMouseEnterLeaveWindow
		{
			get
			{
				return this.m_EventInterests.wantsMouseEnterLeaveWindow;
			}
			set
			{
				this.m_EventInterests.wantsMouseEnterLeaveWindow = value;
				this.MakeParentsSettingsMatchMe();
			}
		}

		internal bool dontClearBackground
		{
			get
			{
				return this.m_DontClearBackground;
			}
			set
			{
				this.m_DontClearBackground = value;
				if (this.m_Parent && this.m_Parent.actualView == this)
				{
					this.m_Parent.backgroundValid = false;
				}
			}
		}

		public bool autoRepaintOnSceneChange
		{
			get
			{
				return this.m_AutoRepaintOnSceneChange;
			}
			set
			{
				this.m_AutoRepaintOnSceneChange = value;
				this.MakeParentsSettingsMatchMe();
			}
		}

		public bool maximized
		{
			get
			{
				return WindowLayout.IsMaximized(this);
			}
			set
			{
				bool flag = WindowLayout.IsMaximized(this);
				if (value != flag)
				{
					if (value)
					{
						WindowLayout.Maximize(this);
					}
					else
					{
						WindowLayout.Unmaximize(this);
					}
				}
			}
		}

		internal bool hasFocus
		{
			get
			{
				return this.m_Parent && this.m_Parent.actualView == this;
			}
		}

		internal bool docked
		{
			get
			{
				return this.m_Parent != null && this.m_Parent.window != null && !this.m_Parent.window.IsNotDocked();
			}
		}

		public static EditorWindow focusedWindow
		{
			get
			{
				HostView hostView = GUIView.focusedView as HostView;
				EditorWindow result;
				if (hostView != null)
				{
					result = hostView.actualView;
				}
				else
				{
					result = null;
				}
				return result;
			}
		}

		public static EditorWindow mouseOverWindow
		{
			get
			{
				HostView hostView = GUIView.mouseOverView as HostView;
				EditorWindow result;
				if (hostView != null)
				{
					result = hostView.actualView;
				}
				else
				{
					result = null;
				}
				return result;
			}
		}

		public Vector2 minSize
		{
			get
			{
				return this.m_MinSize;
			}
			set
			{
				this.m_MinSize = value;
				this.MakeParentsSettingsMatchMe();
			}
		}

		public Vector2 maxSize
		{
			get
			{
				return this.m_MaxSize;
			}
			set
			{
				this.m_MaxSize = value;
				this.MakeParentsSettingsMatchMe();
			}
		}

		[Obsolete("Use titleContent instead (it supports setting a title icon as well).")]
		public string title
		{
			get
			{
				return this.titleContent.text;
			}
			set
			{
				this.titleContent = EditorGUIUtility.TextContent(value);
			}
		}

		public GUIContent titleContent
		{
			get
			{
				GUIContent arg_1C_0;
				if ((arg_1C_0 = this.m_TitleContent) == null)
				{
					arg_1C_0 = (this.m_TitleContent = new GUIContent());
				}
				return arg_1C_0;
			}
			set
			{
				this.m_TitleContent = value;
				if (this.m_TitleContent != null && this.m_Parent && this.m_Parent.window && this.m_Parent.window.rootView == this.m_Parent)
				{
					this.m_Parent.window.title = this.m_TitleContent.text;
				}
			}
		}

		public int depthBufferBits
		{
			get
			{
				return this.m_DepthBufferBits;
			}
			set
			{
				this.m_DepthBufferBits = value;
			}
		}

		[Obsolete("AA is not supported on EditorWindows", false)]
		public int antiAlias
		{
			get
			{
				return 1;
			}
			set
			{
			}
		}

		public Rect position
		{
			get
			{
				return this.m_Pos;
			}
			set
			{
				this.m_Pos = value;
				if (this.m_Parent)
				{
					DockArea dockArea = this.m_Parent as DockArea;
					if (!dockArea)
					{
						this.m_Parent.window.position = value;
					}
					else if (dockArea.parent && dockArea.m_Panes.Count == 1 && !dockArea.parent.parent)
					{
						Rect position = dockArea.borderSize.Add(value);
						if (dockArea.background != null)
						{
							position.y -= (float)dockArea.background.margin.top;
						}
						dockArea.window.position = position;
					}
					else
					{
						dockArea.RemoveTab(this);
						EditorWindow.CreateNewWindowForEditorWindow(this, true, true);
					}
				}
			}
		}

		public EditorWindow()
		{
			this.titleContent.text = base.GetType().ToString();
		}

		[GeneratedByOldBindingsGenerator]
		[MethodImpl(MethodImplOptions.InternalCall)]
		internal extern void MakeModal(ContainerWindow win);

		[ExcludeFromDocs]
		public static EditorWindow GetWindow(Type t, bool utility, string title)
		{
			bool focus = true;
			return EditorWindow.GetWindow(t, utility, title, focus);
		}

		[ExcludeFromDocs]
		public static EditorWindow GetWindow(Type t, bool utility)
		{
			bool focus = true;
			string title = null;
			return EditorWindow.GetWindow(t, utility, title, focus);
		}

		[ExcludeFromDocs]
		public static EditorWindow GetWindow(Type t)
		{
			bool focus = true;
			string title = null;
			bool utility = false;
			return EditorWindow.GetWindow(t, utility, title, focus);
		}

		public static EditorWindow GetWindow(Type t, [DefaultValue("false")] bool utility, [DefaultValue("null")] string title, [DefaultValue("true")] bool focus)
		{
			return EditorWindow.GetWindowPrivate(t, utility, title, focus);
		}

		[ExcludeFromDocs]
		public static EditorWindow GetWindowWithRect(Type t, Rect rect, bool utility)
		{
			string title = null;
			return EditorWindow.GetWindowWithRect(t, rect, utility, title);
		}

		[ExcludeFromDocs]
		public static EditorWindow GetWindowWithRect(Type t, Rect rect)
		{
			string title = null;
			bool utility = false;
			return EditorWindow.GetWindowWithRect(t, rect, utility, title);
		}

		public static EditorWindow GetWindowWithRect(Type t, Rect rect, [DefaultValue("false")] bool utility, [DefaultValue("null")] string title)
		{
			return EditorWindow.GetWindowWithRectPrivate(t, rect, utility, title);
		}

		public void BeginWindows()
		{
			EditorGUIInternal.BeginWindowsForward(1, base.GetInstanceID());
		}

		public void EndWindows()
		{
			GUI.EndWindows();
		}

		internal virtual void OnResized()
		{
		}

		internal void CheckForWindowRepaint()
		{
			double timeSinceStartup = EditorApplication.timeSinceStartup;
			if (timeSinceStartup >= (double)this.m_FadeoutTime)
			{
				if (timeSinceStartup > (double)this.m_FadeoutTime + 1.0)
				{
					this.RemoveNotification();
				}
				else
				{
					this.Repaint();
				}
			}
		}

		internal GUIContent GetLocalizedTitleContent()
		{
			return EditorWindow.GetLocalizedTitleContentFromType(base.GetType());
		}

		internal static GUIContent GetLocalizedTitleContentFromType(Type t)
		{
			EditorWindowTitleAttribute editorWindowTitleAttribute = EditorWindow.GetEditorWindowTitleAttribute(t);
			GUIContent result;
			if (editorWindowTitleAttribute != null)
			{
				string text = "";
				if (!string.IsNullOrEmpty(editorWindowTitleAttribute.icon))
				{
					text = editorWindowTitleAttribute.icon;
				}
				else if (editorWindowTitleAttribute.useTypeNameAsIconName)
				{
					text = t.ToString();
				}
				if (!string.IsNullOrEmpty(text))
				{
					result = EditorGUIUtility.TextContentWithIcon(editorWindowTitleAttribute.title, text);
				}
				else
				{
					result = EditorGUIUtility.TextContent(editorWindowTitleAttribute.title);
				}
			}
			else
			{
				result = new GUIContent(t.ToString());
			}
			return result;
		}

		private static EditorWindowTitleAttribute GetEditorWindowTitleAttribute(Type t)
		{
			object[] customAttributes = t.GetCustomAttributes(true);
			object[] array = customAttributes;
			EditorWindowTitleAttribute result;
			for (int i = 0; i < array.Length; i++)
			{
				object obj = array[i];
				Attribute attribute = (Attribute)obj;
				if (attribute.TypeId == typeof(EditorWindowTitleAttribute))
				{
					result = (EditorWindowTitleAttribute)obj;
					return result;
				}
			}
			result = null;
			return result;
		}

		public void ShowNotification(GUIContent notification)
		{
			this.m_Notification = new GUIContent(notification);
			if (this.m_FadeoutTime == 0f)
			{
				EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Combine(EditorApplication.update, new EditorApplication.CallbackFunction(this.CheckForWindowRepaint));
			}
			this.m_FadeoutTime = (float)(EditorApplication.timeSinceStartup + 4.0);
		}

		public void RemoveNotification()
		{
			if (this.m_FadeoutTime != 0f)
			{
				EditorApplication.update = (EditorApplication.CallbackFunction)Delegate.Remove(EditorApplication.update, new EditorApplication.CallbackFunction(this.CheckForWindowRepaint));
				this.m_Notification = null;
				this.m_FadeoutTime = 0f;
			}
		}

		internal void DrawNotification()
		{
			EditorStyles.notificationText.CalcMinMaxWidth(this.m_Notification, out this.m_NotificationSize.y, out this.m_NotificationSize.x);
			this.m_NotificationSize.y = EditorStyles.notificationText.CalcHeight(this.m_Notification, this.m_NotificationSize.x);
			Vector2 notificationSize = this.m_NotificationSize;
			float num = this.position.width - (float)EditorStyles.notificationText.margin.horizontal;
			float num2 = this.position.height - (float)EditorStyles.notificationText.margin.vertical - 20f;
			if (num < this.m_NotificationSize.x)
			{
				float num3 = num / this.m_NotificationSize.x;
				notificationSize.x *= num3;
				notificationSize.y = EditorStyles.notificationText.CalcHeight(this.m_Notification, notificationSize.x);
			}
			if (notificationSize.y > num2)
			{
				notificationSize.y = num2;
			}
			Rect position = new Rect((this.position.width - notificationSize.x) * 0.5f, 20f + (this.position.height - 20f - notificationSize.y) * 0.7f, notificationSize.x, notificationSize.y);
			double timeSinceStartup = EditorApplication.timeSinceStartup;
			if (timeSinceStartup > (double)this.m_FadeoutTime)
			{
				GUI.color = new Color(1f, 1f, 1f, 1f - (float)((timeSinceStartup - (double)this.m_FadeoutTime) / 1.0));
			}
			GUI.Label(position, GUIContent.none, EditorStyles.notificationBackground);
			EditorGUI.DoDropShadowLabel(position, this.m_Notification, EditorStyles.notificationText, 0.3f);
		}

		internal int GetNumTabs()
		{
			DockArea dockArea = this.m_Parent as DockArea;
			int result;
			if (dockArea)
			{
				result = dockArea.m_Panes.Count;
			}
			else
			{
				result = 0;
			}
			return result;
		}

		internal bool ShowNextTabIfPossible()
		{
			DockArea dockArea = this.m_Parent as DockArea;
			bool result;
			if (dockArea)
			{
				int num = dockArea.m_Panes.IndexOf(this);
				num = (num + 1) % dockArea.m_Panes.Count;
				if (dockArea.selected != num)
				{
					dockArea.selected = num;
					dockArea.Repaint();
					result = true;
					return result;
				}
			}
			result = false;
			return result;
		}

		public void ShowTab()
		{
			DockArea dockArea = this.m_Parent as DockArea;
			if (dockArea)
			{
				int num = dockArea.m_Panes.IndexOf(this);
				if (dockArea.selected != num)
				{
					dockArea.selected = num;
				}
			}
			this.Repaint();
		}

		public void Focus()
		{
			if (this.m_Parent)
			{
				this.ShowTab();
				this.m_Parent.Focus();
			}
		}

		internal void MakeParentsSettingsMatchMe()
		{
			if (this.m_Parent && !(this.m_Parent.actualView != this))
			{
				this.m_Parent.SetTitle(base.GetType().FullName);
				this.m_Parent.autoRepaintOnSceneChange = this.m_AutoRepaintOnSceneChange;
				bool flag = this.m_Parent.depthBufferBits != this.m_DepthBufferBits;
				this.m_Parent.depthBufferBits = this.m_DepthBufferBits;
				this.m_Parent.SetInternalGameViewDimensions(this.m_GameViewRect, this.m_GameViewClippedRect, this.m_GameViewTargetSize);
				this.m_Parent.eventInterests = this.m_EventInterests;
				Vector2 b = new Vector2((float)(this.m_Parent.borderSize.left + this.m_Parent.borderSize.right), (float)(this.m_Parent.borderSize.top + this.m_Parent.borderSize.bottom));
				this.m_Parent.SetMinMaxSizes(this.minSize + b, this.maxSize + b);
				if (flag)
				{
					this.m_Parent.RecreateContext();
				}
			}
		}

		public void ShowUtility()
		{
			this.ShowWithMode(ShowMode.Utility);
		}

		public void ShowPopup()
		{
			if (this.m_Parent == null)
			{
				ContainerWindow containerWindow = ScriptableObject.CreateInstance<ContainerWindow>();
				containerWindow.title = this.titleContent.text;
				HostView hostView = ScriptableObject.CreateInstance<HostView>();
				hostView.actualView = this;
				Rect position = this.m_Parent.borderSize.Add(new Rect(this.position.x, this.position.y, this.position.width, this.position.height));
				containerWindow.position = position;
				containerWindow.rootView = hostView;
				this.MakeParentsSettingsMatchMe();
				containerWindow.ShowPopup();
			}
		}

		internal void ShowWithMode(ShowMode mode)
		{
			if (this.m_Parent == null)
			{
				SavedGUIState savedGUIState = SavedGUIState.Create();
				ContainerWindow containerWindow = ScriptableObject.CreateInstance<ContainerWindow>();
				containerWindow.title = this.titleContent.text;
				HostView hostView = ScriptableObject.CreateInstance<HostView>();
				hostView.actualView = this;
				Rect position = this.m_Parent.borderSize.Add(new Rect(this.position.x, this.position.y, this.position.width, this.position.height));
				containerWindow.position = position;
				containerWindow.rootView = hostView;
				this.MakeParentsSettingsMatchMe();
				containerWindow.Show(mode, true, false);
				savedGUIState.ApplyAndForget();
			}
		}

		public void ShowAsDropDown(Rect buttonRect, Vector2 windowSize)
		{
			this.ShowAsDropDown(buttonRect, windowSize, null);
		}

		internal void ShowAsDropDown(Rect buttonRect, Vector2 windowSize, PopupLocationHelper.PopupLocation[] locationPriorityOrder)
		{
			this.ShowAsDropDown(buttonRect, windowSize, locationPriorityOrder, ShowMode.PopupMenu);
		}

		internal void ShowAsDropDown(Rect buttonRect, Vector2 windowSize, PopupLocationHelper.PopupLocation[] locationPriorityOrder, ShowMode mode)
		{
			this.position = this.ShowAsDropDownFitToScreen(buttonRect, windowSize, locationPriorityOrder);
			this.ShowWithMode(mode);
			this.position = this.ShowAsDropDownFitToScreen(buttonRect, windowSize, locationPriorityOrder);
			this.minSize = new Vector2(this.position.width, this.position.height);
			this.maxSize = new Vector2(this.position.width, this.position.height);
			if (EditorWindow.focusedWindow != this)
			{
				this.Focus();
			}
			this.m_Parent.AddToAuxWindowList();
			this.m_Parent.window.m_DontSaveToLayout = true;
		}

		internal Rect ShowAsDropDownFitToScreen(Rect buttonRect, Vector2 windowSize, PopupLocationHelper.PopupLocation[] locationPriorityOrder)
		{
			Rect result;
			if (this.m_Parent == null)
			{
				result = new Rect(buttonRect.x, buttonRect.yMax, windowSize.x, windowSize.y);
			}
			else
			{
				result = this.m_Parent.window.GetDropDownRect(buttonRect, windowSize, windowSize, locationPriorityOrder);
			}
			return result;
		}

		public void Show()
		{
			this.Show(false);
		}

		public void Show(bool immediateDisplay)
		{
			if (this.m_Parent == null)
			{
				EditorWindow.CreateNewWindowForEditorWindow(this, true, immediateDisplay);
			}
		}

		public void ShowAuxWindow()
		{
			this.ShowWithMode(ShowMode.AuxWindow);
			this.Focus();
			this.m_Parent.AddToAuxWindowList();
		}

		internal void ShowModal()
		{
			this.ShowWithMode(ShowMode.AuxWindow);
			SavedGUIState savedGUIState = SavedGUIState.Create();
			this.MakeModal(this.m_Parent.window);
			savedGUIState.ApplyAndForget();
		}

		private static EditorWindow GetWindowPrivate(Type t, bool utility, string title, bool focus)
		{
			UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(t);
			EditorWindow editorWindow = (array.Length <= 0) ? null : ((EditorWindow)array[0]);
			if (!editorWindow)
			{
				editorWindow = (ScriptableObject.CreateInstance(t) as EditorWindow);
				if (title != null)
				{
					editorWindow.titleContent = new GUIContent(title);
				}
				if (utility)
				{
					editorWindow.ShowUtility();
				}
				else
				{
					editorWindow.Show();
				}
			}
			else if (focus)
			{
				editorWindow.Show();
				editorWindow.Focus();
			}
			return editorWindow;
		}

		public static T GetWindow<T>() where T : EditorWindow
		{
			return EditorWindow.GetWindow<T>(false, null, true);
		}

		public static T GetWindow<T>(bool utility) where T : EditorWindow
		{
			return EditorWindow.GetWindow<T>(utility, null, true);
		}

		public static T GetWindow<T>(bool utility, string title) where T : EditorWindow
		{
			return EditorWindow.GetWindow<T>(utility, title, true);
		}

		public static T GetWindow<T>(string title) where T : EditorWindow
		{
			return EditorWindow.GetWindow<T>(title, true);
		}

		public static T GetWindow<T>(string title, bool focus) where T : EditorWindow
		{
			return EditorWindow.GetWindow<T>(false, title, focus);
		}

		public static T GetWindow<T>(bool utility, string title, bool focus) where T : EditorWindow
		{
			return EditorWindow.GetWindow(typeof(T), utility, title, focus) as T;
		}

		public static T GetWindow<T>(params Type[] desiredDockNextTo) where T : EditorWindow
		{
			return EditorWindow.GetWindow<T>(null, true, desiredDockNextTo);
		}

		public static T GetWindow<T>(string title, params Type[] desiredDockNextTo) where T : EditorWindow
		{
			return EditorWindow.GetWindow<T>(title, true, desiredDockNextTo);
		}

		public static T GetWindow<T>(string title, bool focus, params Type[] desiredDockNextTo) where T : EditorWindow
		{
			T[] array = Resources.FindObjectsOfTypeAll(typeof(T)) as T[];
			T t = (array.Length <= 0) ? ((T)((object)null)) : array[0];
			T result;
			if (t != null)
			{
				if (focus)
				{
					t.Focus();
				}
				result = t;
			}
			else
			{
				t = ScriptableObject.CreateInstance<T>();
				if (title != null)
				{
					t.titleContent = new GUIContent(title);
				}
				for (int i = 0; i < desiredDockNextTo.Length; i++)
				{
					Type desired = desiredDockNextTo[i];
					ContainerWindow[] windows = ContainerWindow.windows;
					ContainerWindow[] array2 = windows;
					for (int j = 0; j < array2.Length; j++)
					{
						ContainerWindow containerWindow = array2[j];
						View[] allChildren = containerWindow.rootView.allChildren;
						for (int k = 0; k < allChildren.Length; k++)
						{
							View view = allChildren[k];
							DockArea dockArea = view as DockArea;
							if (!(dockArea == null))
							{
								if (dockArea.m_Panes.Any((EditorWindow pane) => pane.GetType() == desired))
								{
									dockArea.AddTab(t);
									result = t;
									return result;
								}
							}
						}
					}
				}
				t.Show();
				result = t;
			}
			return result;
		}

		public static void FocusWindowIfItsOpen(Type t)
		{
			UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(t);
			EditorWindow editorWindow = (array.Length <= 0) ? null : (array[0] as EditorWindow);
			if (editorWindow)
			{
				editorWindow.Focus();
			}
		}

		public static void FocusWindowIfItsOpen<T>() where T : EditorWindow
		{
			EditorWindow.FocusWindowIfItsOpen(typeof(T));
		}

		internal void RemoveFromDockArea()
		{
			DockArea dockArea = this.m_Parent as DockArea;
			if (dockArea)
			{
				dockArea.RemoveTab(this, true);
			}
		}

		private static EditorWindow GetWindowWithRectPrivate(Type t, Rect rect, bool utility, string title)
		{
			UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(t);
			EditorWindow editorWindow = (array.Length <= 0) ? null : ((EditorWindow)array[0]);
			if (!editorWindow)
			{
				editorWindow = (ScriptableObject.CreateInstance(t) as EditorWindow);
				editorWindow.minSize = new Vector2(rect.width, rect.height);
				editorWindow.maxSize = new Vector2(rect.width, rect.height);
				editorWindow.position = rect;
				if (title != null)
				{
					editorWindow.titleContent = new GUIContent(title);
				}
				if (utility)
				{
					editorWindow.ShowUtility();
				}
				else
				{
					editorWindow.Show();
				}
			}
			else
			{
				editorWindow.Focus();
			}
			return editorWindow;
		}

		public static T GetWindowWithRect<T>(Rect rect) where T : EditorWindow
		{
			return EditorWindow.GetWindowWithRect<T>(rect, false, null, true);
		}

		public static T GetWindowWithRect<T>(Rect rect, bool utility) where T : EditorWindow
		{
			return EditorWindow.GetWindowWithRect<T>(rect, utility, null, true);
		}

		public static T GetWindowWithRect<T>(Rect rect, bool utility, string title) where T : EditorWindow
		{
			return EditorWindow.GetWindowWithRect<T>(rect, utility, title, true);
		}

		public static T GetWindowWithRect<T>(Rect rect, bool utility, string title, bool focus) where T : EditorWindow
		{
			UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(typeof(T));
			T result;
			if (array.Length > 0)
			{
				result = (T)((object)array[0]);
				if (focus)
				{
					result.Focus();
				}
			}
			else
			{
				result = ScriptableObject.CreateInstance<T>();
				result.minSize = new Vector2(rect.width, rect.height);
				result.maxSize = new Vector2(rect.width, rect.height);
				result.position = rect;
				if (title != null)
				{
					result.titleContent = new GUIContent(title);
				}
				if (utility)
				{
					result.ShowUtility();
				}
				else
				{
					result.Show();
				}
			}
			return result;
		}

		internal static T GetWindowDontShow<T>() where T : EditorWindow
		{
			UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(typeof(T));
			return (array.Length <= 0) ? ScriptableObject.CreateInstance<T>() : ((T)((object)array[0]));
		}

		public void Close()
		{
			if (WindowLayout.IsMaximized(this))
			{
				WindowLayout.Unmaximize(this);
			}
			DockArea dockArea = this.m_Parent as DockArea;
			if (dockArea)
			{
				dockArea.RemoveTab(this, true);
			}
			else
			{
				this.m_Parent.window.Close();
			}
			UnityEngine.Object.DestroyImmediate(this, true);
		}

		public void Repaint()
		{
			if (this.m_Parent && this.m_Parent.actualView == this)
			{
				this.m_Parent.Repaint();
			}
		}

		internal void RepaintImmediately()
		{
			if (this.m_Parent && this.m_Parent.actualView == this)
			{
				this.m_Parent.RepaintImmediately();
			}
		}

		internal void SetParentGameViewDimensions(Rect rect, Rect clippedRect, Vector2 targetSize)
		{
			this.m_GameViewRect = rect;
			this.m_GameViewClippedRect = clippedRect;
			this.m_GameViewTargetSize = targetSize;
			this.m_Parent.SetInternalGameViewDimensions(this.m_GameViewRect, this.m_GameViewClippedRect, this.m_GameViewTargetSize);
		}

		public bool SendEvent(Event e)
		{
			return this.m_Parent.SendEvent(e);
		}

		private void __internalAwake()
		{
			base.hideFlags = HideFlags.DontSave;
		}

		internal static void CreateNewWindowForEditorWindow(EditorWindow window, bool loadPosition, bool showImmediately)
		{
			EditorWindow.CreateNewWindowForEditorWindow(window, new Vector2(window.position.x, window.position.y), loadPosition, showImmediately);
		}

		internal static void CreateNewWindowForEditorWindow(EditorWindow window, Vector2 screenPosition, bool loadPosition, bool showImmediately)
		{
			ContainerWindow containerWindow = ScriptableObject.CreateInstance<ContainerWindow>();
			SplitView splitView = ScriptableObject.CreateInstance<SplitView>();
			containerWindow.rootView = splitView;
			DockArea dockArea = ScriptableObject.CreateInstance<DockArea>();
			splitView.AddChild(dockArea);
			dockArea.AddTab(window);
			Rect position = window.m_Parent.borderSize.Add(new Rect(screenPosition.x, screenPosition.y, window.position.width, window.position.height));
			containerWindow.position = position;
			splitView.position = new Rect(0f, 0f, position.width, position.height);
			window.MakeParentsSettingsMatchMe();
			containerWindow.Show(ShowMode.NormalWindow, loadPosition, showImmediately);
			containerWindow.OnResize();
		}

		[ContextMenu("Add Scene")]
		internal void AddSceneTab()
		{
		}

		[ContextMenu("Add Game")]
		internal void AddGameTab()
		{
		}
	}
}
