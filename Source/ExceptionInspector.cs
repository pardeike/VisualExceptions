﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VisualExceptions
{
	public class ExceptionInspector : MainTabWindow
	{
		internal float scrollViewHeight = 0;
		internal Vector2 scrollPosition = Vector2.zero;

		public ExceptionInspector() : base()
		{
			def = Tab.instance;
			base.resizer = new WindowResizer()
			{
				minWindowSize = new(520, 160)
			};
		}

		public override Vector2 InitialSize => new(600, 600);
		public override Vector2 RequestedTabSize => InitialSize;
		public override float Margin => Columns.spacing;

		public override void DoWindowContents(Rect inRect)
		{
			// do not call base here

			GUI.BeginGroup(inRect);
			var oldAnchor = Text.Anchor;
			var oldFont = Text.Font;

			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.MiddleLeft;

			var title = "Visual Exceptions";
			var titleHeight = title.Size().y;
			var rect = new Rect(0, 0, inRect.width - 16 - Columns.spacing, titleHeight);
			GUI.DrawTexture(rect, Assets.highlight);
			Widgets.Label(rect.Inset(xMin: 4), title);
			inRect = inRect.Inset(yMin: titleHeight + Columns.spacing);

			Text.Font = GameFont.Tiny;
			Text.Anchor = TextAnchor.UpperLeft;

			var viewRect = new Rect(0, 0, inRect.width - 16, scrollViewHeight);
			Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect, true);

			float[] widths;
			var viewWidth = viewRect.width;

			var viewHeight = 0f;
			var exInfos = new Dictionary<ExceptionInfo, int>(ExceptionState.Exceptions);
			foreach (var exInfo in exInfos)
			{
				var details = exInfo.Key.GetReport();
				var count = exInfo.Value;

				if (viewHeight > 0)
				{
					viewHeight += Columns.spacing;
					Columns.Line(viewWidth, ref viewHeight);
					viewHeight += Columns.spacing;
				}

				widths = [0];
				Columns.Row(viewWidth, ref viewHeight, 0, 0, widths, null, exInfo.Key.ExceptionColumn(() => exInfo.Key.GetStacktrace(), count));
				Columns.Row(viewWidth, ref viewHeight, Columns.spacing / 2, Columns.spacing / 2, widths, null, details.topMethod.IconColumn(Assets.location));

				widths =
				[
					details.mods.Select((_, i) => (i + 1).Column(false)).MaxWidth(),
					details.mods.Select(mod => mod.Column()).MaxWidth(),
					0
				];

				details.mods.Iterate((mod, i) => Columns.Row(viewWidth, ref viewHeight, Columns.spacing / 2, 0, widths, () => ChooseMod(mod),
					(i + 1).Column(mod.IsUnpatched()),
					mod.Column(),
					mod.methods.Select(method => method.ShortDescription()).ToList().Column()
				));
			}

			if (Event.current.type == EventType.Layout) scrollViewHeight = viewHeight;
			Widgets.EndScrollView();

			Text.Font = oldFont;
			Text.Anchor = oldAnchor;
			GUI.EndGroup();
		}

		static void ChooseMod(ExceptionDetails.Mod mod)
		{
			var onOff = mod.meta.Active ? "Deactivate this mod (needs restart)" : "Reactivate this mod";
			var options = new List<FloatMenuOption>();
			if (mod.meta.OnSteamWorkshop)
				options.Add(new FloatMenuOption("Open Workshop Page", mod.OpenSteam, MenuOptionPriority.High));
			if (mod.meta.Url.NullOrEmpty() == false)
				options.Add(new FloatMenuOption("Open Website", mod.OpenURL, MenuOptionPriority.High));
			options.Add(new FloatMenuOption(onOff, () => mod.ToggleActive(), mod.meta.Active ? Assets.disableMenu : Assets.enableMenu, Color.white));
			if (mod.IsUnpatched() == false)
				options.Add(new FloatMenuOption("Remove Harmony Patches", () => mod.Unpatch(), Assets.unpatchMenu, Color.white));
			options.Add(new FloatMenuOption(mod.meta.Name, null));
			options.Add(new FloatMenuOption("VersionIndicator".Translate(mod.version), null));
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public override void SetInitialSizeAndPosition()
		{
			base.SetInitialSizeAndPosition();
			doCloseX = true;
			draggable = true;
			resizeable = true;
			if (ExceptionState.configuration.TabToTheRight)
				windowRect.x = UI.screenWidth - windowRect.width;
		}

		public override void Close(bool doCloseSound = true)
		{
			ExceptionState.Clear();
			base.Close(doCloseSound);
		}

		public override void PostClose()
		{
			base.PostClose();
			if (ExceptionState.Exceptions.Count == 0 && Find.UIRoot is UIRoot_Play rootPlay)
			{
				var mainButtons = rootPlay.mainButtonsRoot;
				_ = mainButtons.allButtonsInOrder.RemoveAll(def => def == Tab.instance);
			}
		}
	}
}
