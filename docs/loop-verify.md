为了全面检测monomod patch skill功能和能力, 现在循环执行以下(生成基础项目 - 提出需求 - 编写mod - 验证功能)行
  为:1. 生成各种场景或者边缘条件功能的简单xxx.dll
  2. 提出编写mod功能需求
  3. 编写对应的xxx.patchFunc.mm.dll
  4. 打补丁得到xxx.modded.dll
  5. 验证xxx.modded.dll的功能是否已修改且满足2的需求，如果满足则继续下一步，如果不满足则检查问题并返回第三步
  6. 将本次循环成功验证相关内容记录到文档verify.md上，比如xxx.dll功能，dll原功能代码，patch代码，patch后的dll功能代码,
  验证结果等内容；
  7. 回到第一步重新策划新的测试