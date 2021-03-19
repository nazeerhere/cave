import Comment from "./Comment"
import InputBar from "./InputBar"
import Filter from "./Filter"
import React from "react"

export default function CommentsBar() {
    return(
        <div className="CommentsBar" >
            <p id="comText">
                Comments
            </p>
            <Filter/>
            <Comment/>
            <InputBar/>
        </div>
    )
}