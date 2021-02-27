import React from "react"
import Unity, { UnityContent } from "react-unity-webgl"

export default function Game() {
    const unityContent = new UnityContent(
        "public/TemplateData/UnityProgress.js",
        "Build/UnityLoader.js"
    );
    
    return(
        <div className="Game">
      <div id="unityContainer">
        <Unity unityContent={unityContent}/>
      </div>
        <p>
            some text here yo 
        </p>
        </div>

    )
}