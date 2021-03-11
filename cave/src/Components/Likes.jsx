import React, { useState } from "react"

export default function Likes(liken) {
    const [likes, setLikes] = useState("")
    console.log(liken)

    const addLike = () => {
        let newCount = setLikes(+1)
    }

    return(
        <div>
            <button onClick={addLike} id="likesBtn" >👍🏽 :</button>
            {likes}
        </div>
    )
}