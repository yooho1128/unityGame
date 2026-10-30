"""Shipping integration boundaries for outposts, view-only searches and exact bulk trades."""
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
def source(path):return (ROOT/path).read_text()
def load(name):return json.loads(source('Assets/Resources/Data/'+name+'.json'))
regions={r['regionId']:r for r in load('RegionCatalog')['regions']}
layouts={l['regionId']:l for l in load('RegionLayoutCatalog')['entries']}
hubs=load('JourneyHubCatalog')['entries']
assert {h['regionId'] for h in hubs}=={'glass_coast','inverted_lobby'}
for hub in hubs:
    region=regions[hub['regionId']]
    assert region['act']>=7 and not region.get('isSettlement') and not region.get('isHidden')
    anchors={(p['x'],p['y']) for p in layouts[hub['regionId']]['anchors']}
    assert {(hub['x'],hub['y']),(hub['checkpointX'],hub['checkpointY'])}<=anchors
    assert len(set(hub['itemIds']))==len(hub['itemIds'])>0
assert sum(bool(r.get('isSettlement')) for r in regions.values())==7
runtime=source('Assets/Scripts/Field/ReleaseTerrainPresentation.cs')
assert runtime.index('JourneyHubStation.Ensure')<runtime.index('RegionFieldLayout.Apply')
generator=source('Assets/Editor/PlayablePrologueGenerator.cs')
finish=generator[generator.index('private static void FinishFieldScene'):generator.index('private static Tile PersistLayoutTile')]
assert finish.index('JourneyHubStation.Ensure')<finish.index('RegionFieldLayout.Apply')<finish.index('CreateRegionalDiscoveryContent')
loader=source('Assets/Scripts/Field/MapLoader.cs')
assert 'JourneyHubPolicy.HasServices(destination)' in loader and 'JourneyHubPolicy.Arrival(destination)' in loader
assert loader.index('var operation = TryBeginSceneLoad')<loader.index('bool updateCheckpoint =')
map_ui=source('Assets/Scripts/UI/WorldMapController.cs')
assert 'JourneyHubPolicy.CanFastTravel(_selected)' in map_ui and 'JourneyHubPolicy.Arrival(target)' in map_ui
station=source('Assets/Scripts/Field/JourneyHubStation.cs')
assert 'ReleaseTerrain' in station and 's.name=="Wood"' in station
assert 'name: Wood' in source('Assets/Resources/Art/Environment/ReleaseTerrain.png.meta')
assert 'JourneyHubPolicy.StationCell(hub,occupied)' in station

book=source('Assets/Scripts/UI/ScriptBookController.cs')
party=source('Assets/Scripts/UI/PartyStorageController.cs')
for controller in (book,party):assert 'new CollectionToolbar' in controller and '_query.Apply' in controller
assert book.index('if (state == ScriptBookEntryState.Recorded) recorded++')<book.index('if (!PassesFilter(state)) continue')
assert 'CollectionRewardService.TryClaim(recorded, total' in book
assert 'view.Bind(data, entry.index, state, Select)' in book
assert 'RuntimeUiControls.IsEditingText' in book
assert party.index('foreach (var instance in save.party)')<party.index('_query.Apply(entries)')
assert 'save.party.Sort' not in party and 'save.storage.Sort' not in party
assert '_exchangeIncoming=null;' in party

save=source('Assets/Scripts/Save/SaveManager.cs')
buy=save[save.index('public static bool TryBuyItem(ItemData item,int quantity'):save.index('public static bool CanSellItem')]
sell=save[save.index('public static bool TrySellItem(ItemData item,int quantity'):save.index('public static int MaximumTradeQuantity')]
for transaction in (buy,sell):
    assert transaction.index('QuoteTrade(')<transaction.index('stack.count=quote.ownedAfter')
    assert transaction.index('Current.gold=quote.goldAfter')<transaction.index('InventoryChanged?.Invoke()')
    assert transaction.count('InventoryChanged?.Invoke()')==1 and transaction.count('EconomyChanged?.Invoke()')==1
assert 'TryBuyItem(item,1,out message)' in save and 'TrySellItem(item,1,out message)' in save
dialog=source('Assets/Scripts/UI/ShopTradeDialog.cs')
assert 'SaveManager.TryBuyItem' not in dialog and 'SaveManager.TrySellItem' not in dialog
assert 'transact(item,Count,buying)' in dialog and 'int.TryParse' in dialog
shop=source('Assets/Scripts/UI/SettlementShopController.cs')
assert 'TownServicePolicy.AllowsItem(region,item)||JourneyHubPolicy.AllowsItem(region,item)' in shop
assert 'SaveManager.TryBuyItem(item,quantity' in shop and 'SaveManager.TrySellItem(item,quantity' in shop
for controller in ('FieldInventoryController','SettlementShopController'):
    assert 'new ItemListToolbar' in source('Assets/Scripts/UI/'+controller+'.cs')
back=source('Assets/Scripts/UI/FieldPauseMenuController.cs')
assert 'SettlementShopController.Instance.Back()' in back and 'ScriptBookController.Instance.Back()' in back
assert 'ConvenienceRegression.Validate' in source('Assets/Editor/GameRegressionValidator.cs')
assert 'Bulk purchase exact quantity' in source('Assets/Editor/ReleasePlayModeQa.cs')
print('PASS: 2 outposts / 7 existing towns; generator/runtime anchors; checkpoint/visited travel gates; view-only searches; one-commit trade events; preview/confirmation/back wiring; native regression hooks')
