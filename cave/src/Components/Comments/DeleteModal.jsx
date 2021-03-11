import React from "react"
import axios from "axios"

export default function DeleteModal({ id }) {

    const closeModal = () => {
        document.getElementById("DeleteModal").style.opacity = 0
    }
    
    const handleSubmit = () => {
        axios.delete(`https://uncool-backend.herokuapp.com/comments/${id}`)
        document.getElementById("DeleteModal").style.opacity = 0
    }
    return(
        <div id="DeleteModal" >
            <button onClick={closeModal} >X</button>
            {id}
            <br/>
            are you sure you want to delete this comment?
            <br/>
            <button onClick={handleSubmit} >Delete Comment</button>
        </div>
    )
}