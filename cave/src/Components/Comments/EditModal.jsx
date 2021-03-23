import React, { useState } from "react"
import axios from "axios"

export default function EditModal({ id }) {

    const initialState = {
        user: 3,
        comment: "",
        likes: 0
    }
    const [newComment, setNew] = useState(initialState)
    const [charactersUsed, setChars] = useState(0)

    // fetch(`https://uncool-backend.herokuapp.com/comments/${id}`)
    //     .then(res => setNew(res))

    const handleSubmit = () => {
        let modal = document.getElementById("EditModal")
        modal.style.opacity = 0
        modal.style.zIndex = -1
        document.getElementById("editInput").value = ""
        axios.put(`https://uncool-backend.herokuapp.com/comments/${id}`, 
        newComment
        )
        
        console.log(newComment)
    }

    const closeModal = () => {
        document.getElementById("EditModal").style.opacity = 0
        document.getElementById("EditModal").style.zIndex = -1
    }

    const handleComment = (event) => {
        const userInput = event.target.value
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
        <div id="EditModal" >
            <button onClick={closeModal} className="closerBtn" >X</button>
            <span id="idEdit" >
                #{id}
            </span>
            <span id="editText" >
            Enter new text here: 
            <br/>
            <input type="text"
                    onChange={handleComment}
                    id="editInput"
            />
            <button onClick={handleSubmit} id="Eedit" >
                Apply changes
            </button>
            </span>
        </div>
    )
}