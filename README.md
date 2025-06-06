# lilToon Smart Merge

一个用于Unity编辑器的lilToon材质智能合并工具，可以安全地批量处理和合并lilToon材质参数。

## 功能特性

- **智能备份**: 创建材质克隆并自动重定向Avatar引用
- **选择性合并**: 只复制主材质中明确设置的非贴图属性
- **完全覆写**: 强制复制所有非贴图属性（保留现有贴图）
- **一键还原**: 删除所有克隆并恢复到原始材质
- **批量操作**: 支持同时处理多个材质
- **撤销支持**: 所有操作都支持Unity的撤销功能

## 安装方法

1. 将 `lilToon_Smart_Merge.cs` 文件放入Unity项目的 `Assets/Editor` 文件夹中
2. 确保项目中已安装lilToon着色器

## 使用步骤

### 打开工具
在Unity编辑器中选择菜单: `Tools > lilToon > lilToon Smart Merge`

### 基本流程

1. **设置对象**
   - Avatar Root: 拖入需要处理的Avatar根对象
   - Master Material: 拖入作为模板的主材质

2. **刷新材质列表**
   - 点击 "Refresh Materials" 按钮扫描Avatar中的所有材质

3. **选择目标材质**
   - 在材质列表中勾选需要处理的材质
   - 使用 "Select All"、"Invert"、"Deselect All" 快速选择

4. **执行操作**
   - **Backup**: 备份选中的材质并创建克隆（出现[clone]标识的时候再进行下一步，如果备份后未出现请尝试刷新材质）
   - **Merge Parameters**: 智能合并参数（推荐）
   - **Overwrite**: 完全覆写参数（一般不用）
   - **Restore**: 恢复到原始材质

## 演示

https://github.com/user-attachments/assets/9a0eae2e-6ba8-413b-af5f-9d732ea54372

## 操作说明

### Backup (备份)
- 为每个选中的原始材质创建一个克隆
- 自动将Avatar的材质引用重定向到克隆
- 在克隆的metadata中保存原始路径信息

### Merge Parameters (智能合并)
- 只复制主材质中非默认值的属性
- 保留目标材质的所有贴图
- 不会覆盖空值或默认值

### Overwrite (完全覆写) 
- 强制复制主材质的所有非贴图属性
- 保留目标材质的现有贴图
- 会覆盖所有参数值

### Restore (还原)
- 删除所有克隆材质文件
- 将Avatar的材质引用还原到原始材质
- 清理所有相关标签和数据

## 注意事项

- 建议在操作前保存场景
- 只对克隆材质执行合并/覆写操作
- 原始材质会被妥善保护，不会被直接修改
- 支持撤销操作，但建议谨慎使用
- 工具会自动处理lilToon的关键字设置
