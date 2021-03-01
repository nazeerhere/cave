import React from "react"
import Unity, { UnityContent } from "react-unity-webgl"

export default function Game() {
    const unityContent = new UnityContent({
        progress: "../Dagame/game/Dagame/TemplateData/UnityProgress",
        loaderUrl: "../Dagame/game/Dagame/Build/UnityLoader"
    });
    
    return(
        <div className="Game">

        {/* <div className="unityContainer" >
        </div> */}
            {/* <Unity unityContent={unityContent} height="100%" width="950px" /> */}
            <Unity unityContent={unityContent} id="unityContainer" tabIndex={1}/>

        some text here 
        <input type="text"/>
        </div>

    )
}