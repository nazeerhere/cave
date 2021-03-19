import React, { useState } from "react"

export default function Likes({ comLikes, setLikes }) {
    const addLike = () => {
        setLikes(comLikes + 1)
        console.warn("this is working")
        console.log(comLikes)
    }
    // setLikes(comLikes)

    return(
        <div>
            <button onClick={addLike} id="likesBtn" >👍🏽 :</button>
            {comLikes}
        </div>
    )
}