using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WandEnhancer.Models;

namespace WandEnhancer.Core;

public static class EnhancerConfig
{
    private const int RemoteWebPanelDefaultPort = 3223;
    private static readonly string RemoteWebPanelFallbackUrl = $"http://localhost:{RemoteWebPanelDefaultPort}/remote/";

    public class ResolveContext
    {
        public string Placeholder { get; set; } = null!;
        public Func<string, string?> Handler { get; set; } = null!;
    }

    public class PatchEntry
    {
        public Regex Target { get; set; } = null!;
        public string Patch { get; set; } = null!;
        public string? Name { get; set; }
        public bool Applied { get; set; }
        public bool SingleMatch { get; set; } = true;
        public string[]? CandidateFileNames { get; set; }
        public string[]? SearchHints { get; set; }
        public ResolveContext? Resolver { get; set; }
    }

    public static Dictionary<EPatchType, PatchEntry[]> GetInstance()
    {
        return new Dictionary<EPatchType, PatchEntry[]>()
        {
            {
                EPatchType.ActivatePro,
                new[]
                {
                    new PatchEntry
                    {
                        SearchHints = new[] { "getUserAccount()", "/v3/account" },
                        Resolver = new ResolveContext
                        {
                            Handler = (targetFunction) =>
                            {
                                var fetchMatch = Regex.Match(targetFunction, @"return\s+this\.#(\w+)\.fetch");
                                return fetchMatch.Success ? fetchMatch.Groups[1].Value : null;
                            },
                            Placeholder = "<service_name>"
                        },
                        Name = "getUserAccount",
                        Target = new Regex(@"getUserAccount\(\)\{.*?return\s+this\.#\w+\.fetch\(\{.*?\}\)\}",
                            RegexOptions.Singleline),
                        Patch =
                            "getUserAccount(){return this.#<service_name>.fetch({endpoint:\"/v3/account\",method:\"GET\",name:\"/v3/account\",collectMetrics:0}).then(response=>{response.subscription={period:\"yearly\",state:\"active\"};return response;})}"
                    },
                    new PatchEntry
                    {
                        SearchHints = new[] { "setAccountWandBrandExperience()", "/v3/account/brand_experience_wand" },
                        Resolver = new ResolveContext
                        {
                            Handler = (targetFunction) =>
                            {
                                var match = Regex.Match(targetFunction, @"return\s+this\.#(\w+)\.post");
                                return match.Success ? match.Groups[1].Value : null;
                            },
                            Placeholder = "<service_name>"
                        },
                        Name = "setAccountWandBrandExperience",
                        Target = new Regex(
                            @"setAccountWandBrandExperience\(\)\{.*?return\s+this\.#\w+\.post\(""/v3/account/brand_experience_wand""\)\}",
                            RegexOptions.Singleline),
                        Patch =
                            "setAccountWandBrandExperience(){return this.#<service_name>.post(\"/v3/account/brand_experience_wand\").then(response=>{response.subscription={period:\"yearly\",state:\"active\"};return response;})}"
                    }
                }
            },
            {
                EPatchType.DisableUpdates,
                new[]
                {
                    // Regex consumes 4 closing parens (`)))) `); the 5th (registerHandler's own close)
                    // remains in the original file after replacement. Patch must end with 3 parens — NOT 4.
                    new PatchEntry
                    {
                        CandidateFileNames = new[] { "index.js" },
                        SearchHints = new[] { "ACTION_CHECK_FOR_UPDATE" },
                        Target = new Regex(@"registerHandler\(""ACTION_CHECK_FOR_UPDATE"".*?\)\)\)\)",
                            RegexOptions.Singleline),
                        Patch = "registerHandler(\"ACTION_CHECK_FOR_UPDATE\",(e=>expectUpdateFeedUrl(e,(e=>null)))"
                    }
                }
            },
            {
                EPatchType.DevToolsOnF12,
                new[]
                {
                    new PatchEntry
                    {
                        Name = "devToolsBeforeInputEvent",
                        CandidateFileNames = new[] { "index.js" },
                        SearchHints = new[] { "whenReady().then(" },
                        // Anchor on the Electron main-process `<app>.whenReady().then(`
                        // call. This site is far more stable than the minified renderer
                        // keydown listener that previously held the F12 -> ACTION_OPEN_DEV_TOOLS
                        // dispatch (its identifiers and shape change on every Wand release).
                        // We attach a `before-input-event` hook to every BrowserWindow's
                        // webContents which toggles DevTools on F12 directly from the main
                        // process, bypassing the renderer dispatcher entirely.
                        Target = new Regex(@"(?<app>\w+)\.whenReady\(\)\.then\("),
                        Patch = "${app}.on(\"browser-window-created\",((_,w)=>{try{w.webContents.on(\"before-input-event\",((_,i)=>{if(\"F12\"===i.key&&\"keyDown\"===i.type){w.webContents.isDevToolsOpened()?w.webContents.closeDevTools():w.webContents.openDevTools({mode:\"detach\"})}}))}catch(e){}})),${app}.whenReady().then("
                    }
                }
            },
            {
                EPatchType.RemoteWebPanelPreview,
                new[]
                {
                    new PatchEntry
                    {
                        Name = "remoteBridgeMainBoot",
                        CandidateFileNames = new[] { "index.js" },
                        SearchHints = new[] { "whenReady().then(run)" },
                        Target = new Regex(@"(?<app>\w+)\.whenReady\(\)\.then\(run\)"),
                        Patch = "${app}.whenReady().then(()=>{try{const p=require(\"node:path\");require(p.join(__dirname,\"remote-panel\",\"bridge.cjs\")).installWandRuntime(require(\"electron\"));}catch(e){try{const fs=require(\"node:fs\"),os=require(\"node:os\"),p=require(\"node:path\");fs.appendFileSync(p.join(os.tmpdir(),\"wand-remote-bridge.log\"),\"[\"+new Date().toISOString()+\"] [boot-error] \"+(e&&e.stack||e)+\"\\n\");}catch(_){}}return run()})"
                    },
                    new PatchEntry
                    {
                        Name = "remoteBridgeReset",
                        SearchHints = new[] { "client-state" },
                        Target = new Regex(@"#Je\(\)\{this\.#Oe&&\(this\.#Oe\.dispose\(\),this\.#Oe=null\),this\.#Pe=Date\.now\(\)\.toString\(\),this\.#ke=null,this\.#_e=\[],this\.#Ee=null\}"),
                        Patch = "#Je(){this.#Oe&&(this.#Oe.dispose(),this.#Oe=null),this.#Pe=Date.now().toString(),this.#ke=null,this.#_e=[],this.#Ee=null,this.__wandRemoteTrainerInfo=null,this.__wandRemoteBridge?.sync(null)}"
                    },
                    new PatchEntry
                    {
                        Name = "remoteBridgeSyncSnapshot",
                        SearchHints = new[] { "client-state" },
                        Target = new Regex(@"#Be\(\)\{if\(this\.status===i\.Connected\)\{let e,t=!1,s=this\.#Ee\?\.getMetadata\(h\.vO\)\?\.gameVersion\?\?null,i=!1;const n=this\.#Ve\[this\.#ke\?\?""""\]\|\|null;this\.#Re&&\(e=this\.#Ae\.getPreferredInstallationInfo\(this\.#Re\),e\.app&&\(t=!0,s\?\?=e\.version\?\?null,i=""number""==typeof e\.version&&!this\.#_e\.includes\(e\.version\)\)\),this\.#Me\?\.send\(""client-state"",\{instanceId:this\.#Pe,trainerId:this\.#ke,trainerLoading:this\.#Ee\?\.isLoading\(\),gameInstalled:t,gameVersion:s,needsCompatibilityWarning:i,values:this\.#Ke\(\),themeId:this\.#We,settings:R\(this\.settings\),language:this\.#Ne,accountUuid:this\.account\.uuid,notesReadHash:n,isTimeLimitExpired:""expired""===this\.#Fe\.timerState\}\)\}\}"),
                        Patch = "#Be(){let e,t=!1,s=this.#Ee?.getMetadata(h.vO)?.gameVersion??null,o=!1;const n=this.#Ve[this.#ke??\"\"]||null;this.#Re&&(e=this.#Ae.getPreferredInstallationInfo(this.#Re),e.app&&(t=!0,s??=e.version??null,o=\"number\"==typeof e.version&&!this.#_e.includes(e.version)));this.status===i.Connected&&this.#Me?.send(\"client-state\",{instanceId:this.#Pe,trainerId:this.#ke,trainerLoading:this.#Ee?.isLoading(),gameInstalled:t,gameVersion:s,needsCompatibilityWarning:o,values:this.#Ke(),themeId:this.#We,settings:R(this.settings),language:this.#Ne,accountUuid:this.account.uuid,notesReadHash:n,isTimeLimitExpired:\"expired\"===this.#Fe.timerState});this.__wandRemoteBridge?.sync({instanceId:this.#Pe,trainerId:this.#ke,trainerInfo:this.__wandRemoteTrainerInfo??null,metadata:this.#Ee?.getMetadata(h.vO)??null,trainerLoading:this.#Ee?.isLoading()??false,gameInstalled:t,gameVersion:s,needsCompatibilityWarning:o,language:this.#Ne,themeId:this.#We,notesReadHash:n,isTimeLimitExpired:\"expired\"===this.#Fe.timerState,values:this.#Ke()})}"
                    },
                    new PatchEntry
                    {
                        Name = "remoteBridgeBindHandler",
                        SearchHints = new[] { "client-state" },
                        Target = new Regex(@"setCurrentTrainer\(e,t=null\)\{const s=e\?\.trainerId\|\|null,i=\(s\?e\?\.gameId:null\)\|\|null,n=\(s\?e\?\.supportedVersions:null\)\|\|\[];if\(s===this\.#ke&&t===this\.#Ee\)return;"),
                        Patch = "setCurrentTrainer(e,t=null){this.__wandRemoteBridge||(this.__wandRemoteBridge=(()=>{try{const r=globalThis.require||require;const{ipcRenderer:c}=r(\"electron\");try{c.invoke(\"wand-remote-url\").then((u=>{u&&(globalThis.__wandRemoteBridgeUrl=u)}))}catch(e){}const send=(ch,p)=>{try{return c.invoke(ch,p&&JSON.parse(JSON.stringify(p)))}catch(e){}};return{sync:(s)=>send(\"wand-remote-sync\",s),valueChanged:(s)=>send(\"wand-remote-value-changed\",s),setHandler:(h)=>{if(this.__wandRemoteBridgeBound)return;this.__wandRemoteBridgeBound=true;try{c.invoke(\"wand-remote-set-handler-bind\")}catch(e){}c.on(\"wand-remote-set-value\",(_e,req)=>{try{h(req)}catch(e){}})}}}catch(e){try{const r=globalThis.require||require,fs=r(\"node:fs\"),os=r(\"node:os\"),p=r(\"node:path\");fs.appendFileSync(p.join(os.tmpdir(),\"wand-remote-bridge.log\"),\"[\"+new Date().toISOString()+\"] [renderer-bind-error] \"+(e&&e.stack||e)+\"\\n\");}catch(_){}return null}})());this.__wandRemoteBridge?.setHandler((e=>{if(!this.#Ee||!e?.target)return!1;return this.#Ee.isActive()?this.#Ee.setValue(e.target,e.value,g.kL.Remote,e.cheatId):!1}));this.__wandRemoteTrainerInfo=e??null;const s=e?.trainerId||null,i=(s?e?.gameId:null)||null,n=(s?e?.supportedVersions:null)||[];if(s===this.#ke&&t===this.#Ee)return;"
                    },
                    new PatchEntry
                    {
                        Name = "remoteBridgeValueDelta",
                        SearchHints = new[] { "client-value-changed" },
                        Target = new Regex(@"#ct\(e,t\)\{t\.push\(e\.onValueSet\(e=>\{this\.status===i\.Connected&&e\.source!==g\.kL\.Remote&&this\.#Me\?\.send\(""client-value-changed"",\{instanceId:this\.#Pe,name:e\.name,value:e\.value,cheatId:e\.cheatId\}\)\}\)\),this\.#Be\(\)\}"),
                        Patch = "#ct(e,t){t.push(e.onValueSet(e=>{this.status===i.Connected&&e.source!==g.kL.Remote&&this.#Me?.send(\"client-value-changed\",{instanceId:this.#Pe,name:e.name,value:e.value,cheatId:e.cheatId}),this.__wandRemoteBridge?.valueChanged({trainerId:this.#ke,target:e.name,value:e.value,oldValue:e.oldValue,source:String(e.source??\"desktop\"),cheatId:e.cheatId})})),this.#Be()}"
                    },
                    new PatchEntry
                    {
                        Name = "remoteTooltipPreviewUrl",
                        SearchHints = new[] { "remote_tooltip.scan_the_qr_code_or_visit_the_site", "remote_tooltip.connect_to_wand_remote" },
                        Target = new Regex(@"remoteUrl=""wemodwebsite://remote"""),
                        Patch = "remoteUrl=globalThis.__wandRemoteBridgeUrl||\"" + RemoteWebPanelFallbackUrl + "\""
                    },
                    new PatchEntry
                    {
                        Name = "remoteQrPreviewUrl",
                        SearchHints = new[] { "resources/elements/remote-qr-code" },
                        Resolver = new ResolveContext
                        {
                            Handler = (matchContent) =>
                            {
                                var match = Regex.Match(matchContent, @"this\.canvasElement&&(\w+)\.mo");
                                return match.Success ? match.Groups[1].Value : null;
                            },
                            Placeholder = "<qr_writer>"
                        },
                        Target = new Regex(@"this\.canvasElement&&\w+\.mo\(this\.canvasElement,`\$\{\w+\.A\.wemodWebsiteUrl\}/remote`,this\.options\)"),
                        Patch = "this.canvasElement&&<qr_writer>.mo(this.canvasElement,globalThis.__wandRemoteBridgeUrl||\"" + RemoteWebPanelFallbackUrl + "\",this.options)"
                    }
                }
            }
        };
    }
}