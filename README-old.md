# UnpackKindleHDRes
从 .azw.res中提取高清图片
### 引
事实上，早就有了python脚本可以完成这个过程。只是python脚本运行的各种麻烦让我感到不快，因此重写了C#版本。
（事实上，python2遇到的所有问题都是控制台输出编码的问题，只要把print语句中包含Unicode字符的部分都去掉就可以了）

而我多做了一些其他部分是替换低清图片。

## 如果只需要高清图片
需要：
+ 安装dotnet core
+ 比较新的Kindle for PC。测试版本1.19.3，1.25.几（忘了，反正是最新版然后卸了） 广泛推荐破解用的1.17版本没有高清图，而过高的版本无法破解主要文档（听说是1.25以后），只要插图的话倒是没问题。推荐1.19吧。

在文档或者Documents之类的地方会有一个My Kindle Content文件夹。其中以_EBOK结尾的文件夹里.azw.res结尾的文件就是我们的目标。里面只有单个azw文件的话就没有高清图了（从试读白嫖是不可能的……）。

你可能需要先 生成(以下操作都在项目文件夹里)

` dotnet build `

VSCode的调试里有参数，如果要用VSCode请先修改参数。

修改example.bat，或者你自己写调用。dll后面的参数分别是**资源文件**和**输出文件夹**。请确保输出路径存在且干净。如果不加输出文件夹将会在程序目录建一个文件夹输出。

` dotnet "bin\Debug\netcoreapp2.1\UnpackKindleHDRes.dll" "xxx.azw.res" "HDImage" `

## 替换解包图片

日亚的轻小说（至少我买的里）使用的格式是azw3+.azw.res .后来也找到一篇[博客](https://fireattack.wordpress.com/2018/05/10/dump-images-from-azwazw-res-files-of-kindle/)
印证了这一点（早点查到就不走一样的弯路了orz）：没有直接的格式支持。

这里提供的是一种折衷解决：先使用[KindleUnpack](https://github.com/kevinhendricks/KindleUnpack)的命令(当然你也可以用GUI版，选Dump Mode)

` python2 \KindleUnpack\lib\KindleUnpack.py -d xxx_nodrm.azw3 \temp `

然后

` dotnet "bin\Debug\netcoreapp2.1\UnpackKindleHDRes.dll" "xxx.azw.res" "\temp\mobi8\assembled_text.dat" `

UnpackKindle直接按照Section编号重命名图片来替换原始书籍中类似kindle:embed:0000?mime格式的图片链接，而对应高清图只能按照依据这种原始链接对应回去。
因此我们让KindleUnpack给出原始的文本数据，比对原始数据和生成的xhtml，确定重命名的目标。

目前的缺陷是，由于封面不包含在解包工具提供的原始信息内，我们无法直接重命名封面。虽然剩下来一张肯定就是，但是样本太少也不敢擅自替换，就重命名了一下交给用户手动替换。
这里也不直接替换低清图片，手动替换前可以检查一下，或者选择性不替换（书后面的广告好多啊）

上面搜到的博客里，那位仁兄为了自动化用的是比对图片相似度替换，tql，也是个解决方案吧。

我虽然考虑了自动化（目前理论上可以写个脚本一键完成，需要自己处理一下替换……文件名都处理好了），不过还有些不完善就先不做一键转换这种东西。 

## Tips
KindleUnpack报错处理：报错的都是print，把可能有Unicode字符变量去掉就行。我一共就改了两个地方。
可能在运行前需要先用命令chcp 65001把cmd的代码页改成UTF-8

```
chcp 65001
python2 \KindleUnpack\lib\KindleUnpack.py -d xxx_nodrm.azw3 \temp 
```

DeDRM按照说明使用就行。Kindle for PC别超过1.24

## 未来

我不确定这个项目将会怎样。如果我有足够的闲心也许会整个重写C#版解包让整个过程更方便、明确，也可能转身去基于KindleUnpack写python版的azw3+res合并。但是眼前有期末考也没有新书，那就先放下吧。

PS:python那些莫名其妙的报错真让人没好感……
