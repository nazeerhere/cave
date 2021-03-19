import React, { useState } from "react"
import axios from "axios"

export default function InputBar() {
    const initialState = {
        id: 0,
        user: 3,
        comment: "",
        likes: 0
    }

    const [newComment, setNew] = useState(initialState)
    const [charactersUsed, setChars] = useState(0)

    const handleSubmit = (event) => {
        event.preventDefault();
        console.log(newComment)

        if (charactersUsed <= 300) {
            axios.post(
                "https://uncool-backend.herokuapp.com/comments/",
                newComment
            )
            setNew(initialState)
            console.log("COMMENT SENT")
            document.getElementById("typing").value = ""
            
        } else {
            console.log("over the character limit")
        }
        setChars(0)        
    }

    const handleComment = (event) => {
        const userInput = event.target.value
        console.log(newComment)
        setNew(prevState => {
            return {
                ...prevState,
                comment: userInput
            }
        })
        let newNum = userInput.length
        setChars(newNum)
    }
    return(
        <div className="InputBar" >
            <div className="leaveComment" >
                <p id="charLimit">
                character limit: 
                {charactersUsed} 
                /300
                </p>

                <input type="text"
                    id="typing" 
                    placeholder="Leave a comment.."
                    onChange={handleComment}
                />

                <button id="submit" 
                    onClick={handleSubmit}
                >Submit</button>
            </div>
        </div>
    )
}