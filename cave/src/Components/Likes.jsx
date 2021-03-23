import React, { useState, useEffect } from "react"

export default function Likes({ fakeNumber }) {
    const [comLikes, setLikes] = useState(0)
    // let realNumber = comLikes
    const addLike = () => {
        setLikes(fakeNumber + 1)
        console.warn("this is working")
        console.log(fakeNumber)
    }
    useEffect(() => {
        setLikes(fakeNumber)
    }, [])

    return(
        <div>
            <button onClick={addLike} id="likesBtn" >👍🏽 :</button>
            {comLikes}
        </div>
    )
}