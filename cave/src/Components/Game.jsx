import React from "react"
import Unity, { UnityContent } from "react-unity-webgl"


export default function Game() {
    
    const unityContent = new UnityContent({
        loaderUrl: "build/myunityapp.loader.js",
        dataUrl: "build/myunityapp.data",
        frameworkUrl: "build/myunityapp.framework.js",
        codeUrl: "build/myunityapp.wasm",
    })
    
    return(
        <div className="Game" >
            <Unity unityContent={unityContent}/>
        </div>

    )
}