# Steps-Taken-RNG-Prediction
Steps Taken RNG Prediction for StardewValley

目前唯一依靠模拟随机数消耗实现准确预测每日运气的模组

todo：
补充雷雨转化前更新日常任务板带来的随机数消耗（至少debug时显示消耗了）
Game1.RefreshQuestOfTheDay()
→ItemDeliveryQuest.reloadDescription()
→Quest.loadQuestInfo()
→ItemRegistry.Create()
→ObjectDataDefinition.CreateItem()
→new Object()
→Game1.random.NextBool()
