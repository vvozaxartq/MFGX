# AutoTestSystem
  
**AutoTestSystem is a MFG architecture from Liteon MTE team 2024**  
AutoTestSystem is refers to the graphical user interface (GUI) used in manufacturing industries to enable workers to interact with digital systems and track production processes. The AutoTestSystem UI is a user interface that displays real-time data, such as machine status and work orders, on a visual dashboard, allowing operators to quickly identify and resolve issues in the production process.  (by Jordan Song)
  
## Install
> • [Visual Studio 2019 community version](https://learn.microsoft.com/zh-tw/visualstudio/releases/2019/release-notes)  
> 
> • [NetFramework 4.7.2](https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/thank-you/net472-developer-pack-offline-installer)  
> 
> • [Microsoft Access Database Engine 2016](https://www.microsoft.com/zh-tw/download/details.aspx?id=54920) have x64 & x32 version, you need to used cmd command & keyin **AccessDatabaseEngine.exe /quiet** to install both.  
> 
> • D disk (or you need to change the log path in AutoTestSystem\Config\Config.ini)  


## Run
> • Please run the **AutoTestSystem.sln**  
> 
> • Please use **Release & x64 compiler** if you want to release to product line.
> 
> • Please keyin "Enter" in the SN text_label to run the program if you want to run UI.  
> 

## Edit
> • Please update the **SW version** before you update to git (path:AutoTestSystem/Properties/AssemblyInfo.cs), For example: 
> 1. [assembly: AssemblyVersion("2.5.0.0")]
> 2. [assembly: AssemblyFileVersion("2.5.0.0")]
> 
> • If you want to add any **exist equipment**, please follow this step:
> 1. Add the equipment code in the /Script
> 2. Add the equipment name in the /Equipment
> (ex: If you need to add CCD script item, Base\CCD --> Equipment\CCD\CCDBase.cs)  
> 
> • If you want to add any **new equipment**, please
> 1. Add the Combo box Click in the RecipeManagement.cs
> 2. Add the equipment type in the /Base
> 3. Add the equipment code in the /Script
> 4. Add the equipment name in the /Equipment
> 
> • If you want to add any **DUT**
> 1. Add the DUT name in the /DUT
> 


## Structor
> • AutoTestSystem  (UI, design)  
> • Resources       (UI, image)  
> • Properties      (UI, version & setting) 
>   
> • Model           (Function, core function)  
> • Base            (Function, abstract level)  
> • Script          (Function, practical level, including Script & Function interface) 
> • DAL             (Function, practical level, communication)  
> • Equipment       (Function, user type level, Equipment Function)  
> • DUT             (Function, user type level, Device)  
>   
> • dll             (Plugin, library for UI)  
> • lib             (Plugin, library for Equipment or Device ) 
>   
> • Config          (File, Script setting ini, json & excel)  
> • BLL             (Other function & Helper & log)  
> • Utility         (Other)  
> 


## Helper
> • If you cannot open excel file on C# program, please follow these step:  
> 1. create the Test.json 
> 2. put the Test.json to this path autotestsystem\AutoTestSystem\bin\x64\Debug\Config\Test.json.
> 3. change the **STATIONNAME=Test** in the config.ini 
>  
> • If you have **Error： Load_Devices File not found**, please follow these step:  
> 1. check the **DeviceListPath = Config\Devices\Devices.json** in the config.ini 
> 2. open the path autotestsystem\AutoTestSystem\bin\x64\Debug\Config\Devices
> 3. check the json file **Devices.json**  
> 
> • If you have error "請確認已經參考包含此類型的組件。如果此類型是您開發專案的一部分，請確認此專案是否已使用您目前平台的設定或 [Any CPU] 成功建置。" when you open the RecipeManagement UI. 
> 1. Please use **ANY CPU** not x64 CPU, and you can see the Qbutton.  
>  

## UI

![markdown](https://i.imgur.com/l7KSwON.jpg "markdown")  
![markdown](https://i.imgur.com/LRdYcvU.jpg "markdown")  
![markdown](https://i.imgur.com/bKJdhcF.jpg "markdown")  
