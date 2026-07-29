import json

with open('Assets/Editor/LayoutDump.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

for node in data['elements']:
    name = node['name']
    if name in ['GEARTab', 'BAGTab', 'SKILLSTab', 'GearPanelUI', 'SkillsPanelUI', 'MainInventoryUI', 'SecureInventoryUI', 'TrashCanUI', 'RewardInventoryUI']:
        pos = node['anchoredPosition']
        size = node['sizeDelta']
        print(f"{name}: Pos({pos['x']}, {pos['y']}), Size({size['x']}, {size['y']})")
