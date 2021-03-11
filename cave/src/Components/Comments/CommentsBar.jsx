import React from "react"
import Comment from "./Comment"

export default function CommentsBar() {
    return(
        <div className="CommentsBar" >
            <p id="comText">
                Comments
            </p>
            <Comment/>
        </div>
    )
}