import React, { useState } from "react"

export default function Likes(props) {
    const [likes, setLikes] = useState(props)

    const addLike = () => {
        let newCount = setLikes(+1)
    }

    return(
        <div>
            likes:
            {likes}
            <button onClick={addLike} >A button</button>
        </div>
    )
}