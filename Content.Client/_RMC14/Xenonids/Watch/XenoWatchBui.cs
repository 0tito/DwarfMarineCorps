using System.Linq;
using System.Text.RegularExpressions;
using Content.Client._RMC14.Xenonids.UI;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Destrain;
using Content.Shared._RMC14.Xenonids.Strain;
using Content.Shared._RMC14.Xenonids.Watch;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.LineEdit;

namespace Content.Client._RMC14.Xenonids.Watch;

[UsedImplicitly]
public sealed class XenoWatchBui : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;

    private static readonly Comparer<EntityPrototype> EntityComparer =
        Comparer<EntityPrototype>.Create(static (a, b) =>
        {
            var name = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
            return name != 0 ? name : string.Compare(a.ID, b.ID, StringComparison.Ordinal);
        });

    private static readonly Color XenoColor = Color.FromHex("#800080");
    private static readonly Color XenoHighlightColor = Color.FromHex("#9E379E");

    private readonly SortedDictionary<int, SortedSet<EntityPrototype>> _tiers = new();
    private readonly Dictionary<string, string> _casteOf = new();
    private readonly Dictionary<string, bool> _shownXenos = new();
    private readonly Dictionary<string, int> _xenoCounts = new();
    private readonly Dictionary<NetEntity, XenoChoiceControl> _controls = new();
    private readonly Dictionary<string, Texture?> _textures = new();
    private Texture? _leaderIconTexture;

    private bool _usingSearchBar;
    private string _searchBarText = string.Empty;

    [ViewVariables]
    private XenoWatchWindow? _window;

    private readonly SpriteSystem _sprite;

    public XenoWatchBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _sprite = EntMan.System<SpriteSystem>();
    }

    protected override void Open()
    {
        base.Open();
        var window = EnsureWindow();
        _window = window;

        foreach (var entity in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (entity.Abstract ||
                !entity.TryGetComponent(out XenoComponent? xeno, _compFactory))
            {
                continue;
            }

            var caste = GetCasteId(entity);
            _casteOf[entity.ID] = caste;

            // Strains and hidden variants are counted under their base caste, not as their own entry.
            if (entity.ID != caste ||
                entity.TryGetComponent<XenoStrainComponent>(out _, _compFactory) ||
                entity.TryGetComponent<XenoHiddenComponent>(out _, _compFactory) ||
                !xeno.ShowInWatchWindowCounts)
            {
                continue;
            }

            if (!_tiers.TryGetValue(xeno.Tier, out var xenos))
            {
                xenos = new SortedSet<EntityPrototype>(EntityComparer);
                _tiers.Add(xeno.Tier, xenos);
            }

            xenos.Add(entity);
        }

        foreach (var (tier, xenos) in _tiers.Reverse())
        {
            if (tier > 3)
                continue;

            var row = new XenoTierRow();
            row.SlotsLeft.Visible = tier is 2 or 3;
            row.TierButton.OnToggled += OnTierToggled;
            row.SetInfo(0, tier);
            row.Tier = tier;

            foreach (var xeno in xenos)
            {
                var xenoControl = new XenoHiveCountControl();
                var xenoButton = new Button
                {
                    Text = xeno.Name,
                    Name = xeno.ID,
                    ToggleMode = true,
                    ModulateSelfOverride = XenoColor,
                };
                xenoButton.AddStyleClass(StyleBase.ButtonSquare);
                xenoButton.OnToggled += OnButtonToggled;
                xenoControl.XenoButton.AddChild(xenoButton);

                row.XenosContainer.AddChild(xenoControl);
                _shownXenos.Add(xeno.ID, false);
                _xenoCounts.Add(xeno.ID, 0);
            }

            window.RowContainer.AddChild(row);
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not XenoWatchBuiState s)
            return;

        _window = EnsureWindow();
        _window.BurrowedLarvaLabel.Text = $"Burrowed Larva: {s.BurrowedLarva}";
        _window.XenoCount.Text = $"Total Sisters: {s.XenoCount}";

        foreach (var key in _xenoCounts.Keys)
        {
            _xenoCounts[key] = 0;
        }

        var seen = new HashSet<NetEntity>();
        var ordered = new List<XenoChoiceControl>(s.Xenos.Count);
        foreach (var xeno in s.Xenos.OrderByDescending(x => x.Queen).ThenByDescending(x => x.King).ThenByDescending(x => x.Leader))
        {
            seen.Add(xeno.Entity);

            if (!_controls.TryGetValue(xeno.Entity, out var control))
            {
                var entity = xeno.Entity;
                control = new XenoChoiceControl();
                control.Button.OnPressed += _ => SendPredictedMessage(new XenoWatchBuiMsg(entity));
                control.HealButton.OnPressed += _ => SendPredictedMessage(new XenoWatchBuiHealingMsg(entity));
                control.PlasmaButton.OnPressed += _ => SendPredictedMessage(new XenoWatchBuiTransferPlasmaMsg(entity));
                control.LeaderButton.OnPressed += _ => SendMessage(new XenoWatchBuiLeaderMsg(entity));
                _controls.Add(entity, control);
            }

            Texture? texture = null;
            if (xeno.Id is { } id)
            {
                if (!_textures.TryGetValue(id.Id, out texture))
                {
                    if (_prototype.TryIndex(id, out var evolution))
                        texture = _sprite.Frame0(evolution);

                    _textures[id.Id] = texture;
                }
            }

            var rawId = xeno.Id?.Id;
            var caste = rawId != null && _casteOf.TryGetValue(rawId, out var mapped)
                ? mapped
                : xeno.StrainOf?.Id ?? rawId ?? string.Empty;

            control.Set(xeno.Name, texture);
            control.SetName(caste);

            if (_xenoCounts.ContainsKey(caste))
                _xenoCounts[caste] += 1;

            control.SetHealth((float) xeno.Health);
            control.SetPlasma((float) xeno.Plasma);
            control.SetEvo((int) xeno.Evo);

            control.SetRank(xeno.Queen, xeno.King, xeno.Leader, _leaderIconTexture);

            control.QueenButtons.Visible = s.CanSetLeader;
            control.LeaderButton.Visible = s.CanSetLeader;
            control.HealPlasmaButtons.Visible = s.IsQueen;
            control.SetLeaderButton(xeno.Leader);

            if (s.CanSetLeader)
            {
                var nameWidth = _window.Width - 310;
                if (float.IsFinite(nameWidth) && nameWidth > 0 && control.NameLabel.MinWidth != nameWidth)
                    control.NameLabel.SetWidth = nameWidth;
            }

            ordered.Add(control);
        }

        List<NetEntity>? stale = null;
        foreach (var key in _controls.Keys)
        {
            if (seen.Contains(key))
                continue;

            stale ??= new List<NetEntity>();
            stale.Add(key);
        }

        if (stale != null)
        {
            foreach (var key in stale)
            {
                _controls[key].Orphan();
                _controls.Remove(key);
            }
        }

        var reorder = _window.XenoContainer.ChildCount != ordered.Count;
        if (!reorder)
        {
            var i = 0;
            foreach (var child in _window.XenoContainer.Children)
            {
                if (!ReferenceEquals(child, ordered[i]))
                {
                    reorder = true;
                    break;
                }

                i++;
            }
        }

        if (reorder)
        {
            _window.XenoContainer.RemoveAllChildren();
            foreach (var control in ordered)
                _window.XenoContainer.AddChild(control);
        }

        foreach (var child in _window.RowContainer.Children)
        {
            if (child is not XenoTierRow row)
                continue;

            var slots = row.Tier switch
            {
                2 => s.TierTwoSlots,
                3 => s.TierThreeSlots,
                _ => FixedPoint2.Zero,
            };

            if (row.Tier is 2 or 3)
                row.SlotsLeft.Text = $"Slots left: {slots}";

            foreach (var hiveChild in row.XenosContainer.Children)
            {
                if (hiveChild is not XenoHiveCountControl hive)
                    continue;

                var name = string.Empty;
                foreach (var button in hive.XenoButton.Children)
                {
                    if (button is Button xenoButton)
                        name = xenoButton.Name ?? string.Empty;
                }

                if (_xenoCounts.TryGetValue(name, out var count))
                    hive.Count.Text = $"{count}";
            }
        }

        UpdateList();
    }

    private XenoWatchWindow EnsureWindow()
    {
        if (_window != null)
            return _window;

        _window = this.CreateWindow<XenoWatchWindow>();
        _window.SearchBar.OnTextChanged += OnSearchBarChanged;
        _window.StopWatchingButton.OnPressed += _ => SendMessage(new XenoWatchBuiStopMsg());
        _leaderIconTexture ??= _sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/xeno_leader.rsi/"), "hudxenoleader"));
        return _window;
    }

    private static readonly Regex DashesAndDigitsPattern = new Regex("[-\\d]+");

    /// <summary>
    /// <c>RemoveDashesAndDigits</c> returns a copy of the input string with all hyphen <c>-</c> and digit <c>0-9</c>
    /// characters removed. This allows for a smarter search in the Watch search menus.
    /// <param name="name">The name of a humanoid or xenonid character</param>
    /// <example>
    /// <c>"KE-112-E"</c> returns <c>"KEE"</c>,
    /// <c>"XX-42"</c> returns <c>"XX"</c>,
    /// <c>"HEK"</c> returns <c>"HEK"</c>,
    /// <c>"HEK-891"</c> returns <c>"HEK"</c>,
    /// <c>"RO-123-CK"</c> returns <c>"ROCK"</c>,
    /// <c>"Shakes-The-Friendlies"</c> returns <c>"ShakesTheFriendlies"</c>,
    /// <c>"Zachary Randolf"</c> returns <c>"Zachary Randolf"</c>.
    /// </example>
    /// </summary>
    public static string RemoveDashesAndDigits(string name)
    {
        return DashesAndDigitsPattern.Replace(name, "");
    }

    private static bool ShowSearchResult(string search, string result)
    {
        return result.Contains(search, StringComparison.OrdinalIgnoreCase)
               || RemoveDashesAndDigits(result).Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void OnSearchBarChanged(LineEditEventArgs args)
    {
        if (_window is not { Disposed: false })
            return;

        _searchBarText = args.Text;

        if (string.IsNullOrWhiteSpace(args.Text))
        {
            _usingSearchBar = false;
            foreach (var child in _window.XenoContainer.Children)
            {
                if (child is XenoChoiceControl control)
                    control.Visible = true;
            }

            UpdateList();
            return;
        }

        _usingSearchBar = true;
        UpdateList();
    }

    private void OnButtonToggled(BaseButton.ButtonEventArgs args)
    {
        var name = args.Button.Name ?? string.Empty;
        if (name == string.Empty)
            return;

        if (_shownXenos.ContainsKey(name))
        {
            _shownXenos[name] = !_shownXenos[name];
            args.Button.ModulateSelfOverride = UpdateButtonColor(_shownXenos[name]);
        }

        ClearSearchBar();
        UpdateList();
    }

    private void OnTierToggled(BaseButton.ButtonEventArgs args)
    {
        args.Button.ModulateSelfOverride = UpdateButtonColor(args.Button.Pressed);

        if (args.Button.Name != null &&
            int.TryParse(args.Button.Name.Split()[1], out var tier) &&
            _tiers.TryGetValue(tier, out var xenos))
        {
            foreach (var xeno in xenos)
            {
                if (_shownXenos.ContainsKey(xeno.ID))
                    _shownXenos[xeno.ID] = args.Button.Pressed;
            }
        }

        UpdateTierChildren();
        ClearSearchBar();
        UpdateList();
    }

    private void UpdateTierChildren()
    {
        if (_window is not { Disposed: false })
            return;

        foreach (var child in _window.RowContainer.Children)
        {
            if (child is not XenoTierRow row)
                continue;

            foreach (var hiveChild in row.XenosContainer.Children)
            {
                if (hiveChild is not XenoHiveCountControl countControl)
                    continue;

                foreach (var button in countControl.XenoButton.Children)
                {
                    if (button is not Button xenoButton)
                        continue;

                    var name = xenoButton.Name ?? string.Empty;
                    if (name != string.Empty && _shownXenos.TryGetValue(name, out var shown))
                        xenoButton.ModulateSelfOverride = UpdateButtonColor(shown);
                }
            }
        }
    }

    private static Color UpdateButtonColor(bool state)
    {
        return state ? XenoHighlightColor : XenoColor;
    }

    private void ClearSearchBar()
    {
        if (_window is not { Disposed: false })
            return;

        _window.SearchBar.Clear();
        _usingSearchBar = false;
    }

    private string GetCasteId(EntityPrototype prototype)
    {
        if (prototype.TryGetComponent<XenoDestrainComponent>(out var destrain, _compFactory))
            return destrain.DestrainTo.Id;

        var current = prototype;
        while (FindConcreteXenoAncestor(current) is { } ancestor)
            current = ancestor;

        return current.ID;
    }

    private EntityPrototype? FindConcreteXenoAncestor(EntityPrototype prototype)
    {
        foreach (var parentId in prototype.Parents ?? [])
        {
            if (!_prototype.TryIndex<EntityPrototype>(parentId, out var parent))
                continue;

            if (!parent.Abstract && parent.TryGetComponent<XenoComponent>(out _, _compFactory))
                return parent;

            if (FindConcreteXenoAncestor(parent) is { } ancestor)
                return ancestor;
        }

        return null;
    }

    private void UpdateList()
    {
        if (_window is not { Disposed: false })
            return;

        foreach (var child in _window.XenoContainer.Children)
        {
            if (child is not XenoChoiceControl control)
                continue;

            if (_usingSearchBar)
            {
                control.Visible = ShowSearchResult(_searchBarText, control.NameLabel.GetMessage() ?? string.Empty);
                continue;
            }

            if (!_shownXenos.ContainsValue(true))
            {
                control.Visible = true;
                continue;
            }

            control.Visible = _shownXenos.TryGetValue(control.Button.Name ?? string.Empty, out var shown) && shown;
        }
    }
}
