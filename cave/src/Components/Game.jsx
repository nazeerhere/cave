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

            <div className="controlsModal" >
                "W" and "X" keys are for attacking
                <br/>
                <br/>
                left and right keys are the "A" key and the right arrow
                <br/>
                <br/>
                jump keys are "Space" and the up arrow
            </div>
        </div>

    )
}